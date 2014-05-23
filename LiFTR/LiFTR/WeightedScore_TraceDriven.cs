//#define _FILE_SYSTEM_INFO
//#define _BASE_LINE
//#define _HYBRID

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Dec0de.Bll;

namespace LiFTR
{
    class WeightedScore_TraceDriven
    {
        private string _input_CSV_path;
        private string _paths_vtf_file;
        private string _accuracy_numbers_CSV_path;
        private string _offset_map_path;
        private string _ground_truth_token_file_path;
        private string _dictionary_path;
        private string _pre_pop_path;
       
        private List<long> _ground_truth_true_offsets;
        private TextRanker _bse;
        private Dictionary<int, List<long>> _block_offset_mapping_initial_sorted;
        Dictionary<int, List<string>> _block_CSV_line_mapping;
        private HashSet<int> _TP_blocks;
        private InvestigatorFeedbackSorting _ifs;
        
        private List<int> _shown_blocks;
        private Dictionary<string, bool> _good_accounted_tokens;       
        private Dictionary<string, bool> _bad_accounted_tokens;

        private Dictionary<int, Block_Vector> _bvectors;

        private Dictionary<string, double> _bad_block_features;        

        int _total_num_of_feedbacks;
        int _num_of_top_ranks_to_track;
        int _tp_among_shown_blocks;

        private Dictionary<string, bool> _shown_field_string;

        private FileSystemInfo _fso = null;
        private InvestigatorInput _investigator_input = null;
        private BaseLine _baseline = null;

        List<int> _upper_halve_TP_ranks = null;
        string _rank_writer_path = null;
        StreamWriter _rank_vector_wr = null;

        private Dictionary<int, List<string>> _sorted_blocks;
        public Dictionary<int, double> _block_scores;   

        public WeightedScore_TraceDriven(int total_feedbacks_to_take, List<string> arguments)
        {
            _input_CSV_path = arguments[0];      
            _ground_truth_token_file_path = arguments[1];
            _dictionary_path = arguments[2];
            _pre_pop_path = arguments[3];
            _accuracy_numbers_CSV_path = arguments[4];
            _rank_writer_path = arguments[5];
            if (arguments.Count == 7)
            {
                _offset_map_path = arguments[6];   
            }

            _paths_vtf_file = @"yaffs2-droid-eris-postpopulation_sans_oob.nanddump_paths.vtf";
            _ground_truth_true_offsets = new List<long>();
            Load_Ground_Truth_Offsets();
            _bad_block_features = new Dictionary<string, double>();
            _block_offset_mapping_initial_sorted = new Dictionary<int, List<long>>();
            Load_Block_Offset_Mapping();
            _ifs = new InvestigatorFeedbackSorting(_input_CSV_path, _paths_vtf_file, _block_offset_mapping_initial_sorted, _bad_block_features);
            _TP_blocks = new HashSet<int>();            
            List_Good_Blocks();
            Console.WriteLine("TP:{0}, total:{1}", _TP_blocks.Count, _block_offset_mapping_initial_sorted.Keys.Count);
            //Environment.Exit(0);
            _num_of_top_ranks_to_track = (int) Math.Ceiling(1.1 * _TP_blocks.Count);
            
            _shown_blocks = new List<int>();
            _good_accounted_tokens = new Dictionary<string, bool>();
            _bad_accounted_tokens = new Dictionary<string, bool>();

            _bvectors = new Dictionary<int, Block_Vector>();
            Create_Block_Vectors();
           
            _total_num_of_feedbacks = total_feedbacks_to_take;            
            _tp_among_shown_blocks = 0;

            _shown_field_string = new Dictionary<string, bool>();
            _upper_halve_TP_ranks = new List<int>();

            _rank_vector_wr = new StreamWriter(_rank_writer_path);
            _rank_vector_wr.AutoFlush = true;
        }

        public static void Main(string[] args)
        {
            if (args.Length < 5 || args.Length > 7)
            {
                Console.WriteLine("\nInvalid use!\n\n");
                Console.WriteLine("Usage: TraceDriven.exe <inference csv path> <ground truth token path> <dictionary path> <pre-population inference path>" +
                    " <number of top clusters to track> <offset map path>");
                return;
            }
            List<string> arguments = new List<string>(args.ToList());            
            WeightedScore_TraceDriven ws_trace_driven = new WeightedScore_TraceDriven(20, arguments);
            ws_trace_driven.Begin_Experiment();            
            ws_trace_driven.Write_Remaining_Bvectors();
            ws_trace_driven._rank_vector_wr.Close();
        }

        private void Begin_Experiment()
        {
            Write_rank_vector_at_zero_feedback();            
            int block_count = _bvectors.Count;
            int feedbacks_taken = 0;
            List<int> prev_rvector = new List<int>();

            for (int feedback_ind = 0; feedback_ind < Math.Min(_total_num_of_feedbacks, block_count); feedback_ind++)
            {
                int block_to_show = _bvectors.Keys.ElementAt(0);

                _shown_blocks.Add(block_to_show);

                bool isTP = _TP_blocks.Contains(block_to_show);
                
                List<long> tp_offsets = _block_offset_mapping_initial_sorted[block_to_show].Where(x => _ground_truth_true_offsets.Contains(x)).ToList();
                List<long> fp_offsets = new List<long>(_block_offset_mapping_initial_sorted[block_to_show].Where(x => !_ground_truth_true_offsets.Contains(x)).ToList());
                _bvectors.Remove(block_to_show);
                Update_Sort_bvectors(tp_offsets, fp_offsets);
                
                prev_rvector = Update_Rank_Vectors(feedback_ind + 1, isTP);
                feedbacks_taken++;
            }

            if (block_count < _total_num_of_feedbacks)
            {
                for (int i = 0; i < _total_num_of_feedbacks - block_count; i++)
                {
                    Write_Rank_Vector(prev_rvector);
                }
            }
        }        

        #region Accuracy Numbers Calculator

        void Write_rank_vector_at_zero_feedback()
        {
            List<int> tp_ranks = new List<int>();
            for (int i = 0; i < Math.Min(_bvectors.Count, 1100); i++)
            {
                int block = _bvectors.Keys.ElementAt(i);
                if (_TP_blocks.Contains(block))
                {
                    tp_ranks.Add(i + 1);
                }
            }
            Write_Rank_Vector(tp_ranks);
        }

        List<int> Update_Rank_Vectors(int feedbacks_taken, bool was_block_tp)
        {
            List<int> lower_halve_tp_ranks = new List<int>(); // this keeps varying with feedback (so not made global)
            if (was_block_tp)
            {
                _tp_among_shown_blocks++;
                _upper_halve_TP_ranks.Add(feedbacks_taken);
            }           
           
            for (int i = 0; i < Math.Min(_bvectors.Count, 1100); i++)
            {
                int block = _bvectors.Keys.ElementAt(i);
                if (_TP_blocks.Contains(block))
                {
                    lower_halve_tp_ranks.Add(_upper_halve_TP_ranks.Count + i + 1);
                }
            }       

            List<int> r_vector = new List<int>();
            List<int> actual_positions_of_upper_halve_TP_pages = new List<int>();
            for (int i = 0; i < _upper_halve_TP_ranks.Count; i++)
            {
                actual_positions_of_upper_halve_TP_pages.Add(i + 1);
            }
            r_vector.AddRange(actual_positions_of_upper_halve_TP_pages);
            r_vector.AddRange(lower_halve_tp_ranks);
            Write_Rank_Vector(r_vector);
            return r_vector;
        }       

        #endregion

        #region Block Vector Operations

        private void Create_Block_Vectors()
        {
            List<int> all_blocks = new List<int>(_block_offset_mapping_initial_sorted.Keys);
            int valid_token_count = 0;
            for (int i = 0; i < all_blocks.Count; i++)
            {
                if (_ifs._block_content_mapping.ContainsKey(all_blocks[i]))
                {
                    List<string> all_tokens = new List<string>(_ifs._block_content_mapping[all_blocks[i]]);
                    List<string> valid_tokens = new List<string>(all_tokens);

                    valid_tokens.RemoveAll(x => _bad_block_features.ContainsKey(x));
                    valid_tokens.RemoveAll(x => x.Length < 3);
                    valid_tokens.RemoveAll(x => x.Length == 3 && !_bse._dict_words.ContainsKey(x));
                    valid_tokens.RemoveAll(x => _bse.Is_Field_Small_Phone_Num(x) == true);

                    valid_token_count = valid_tokens.Count;
                }

                Block_Vector bv = new Block_Vector(all_blocks[i], _block_scores[all_blocks[i]], valid_token_count, _sorted_blocks[all_blocks[i]].Count);
                _bvectors.Add(all_blocks[i], bv);
            }            
            _bvectors = (from entry in _bvectors orderby entry.Value._net_score descending select entry).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        private List<List<long>> Get_sampled_offsets(List<long> tp_offsets, List<long> fp_offsets)
        {
            List<long> all_offsets = new List<long>();
            all_offsets.AddRange(tp_offsets);
            all_offsets.AddRange(fp_offsets);
            Random rand = new Random();
            List<long> sampled_all_offsets = new List<long>();
            for (int i = 0; i < Math.Min(all_offsets.Count, 10); i++)
            {
                int ind = rand.Next(0, all_offsets.Count);
                long offs = all_offsets[ind];
                if (!sampled_all_offsets.Contains(offs))
                {
                    sampled_all_offsets.Add(offs);
                }
                else
                {
                    i--;
                }
            }

            List<long> sampled_tp_offsets = new List<long>();
            
            List<long> sampled_fp_offsets = new List<long>();           

            for (int i = 0; i < sampled_all_offsets.Count; i++)
            {
                if (tp_offsets.Contains(sampled_all_offsets[i]))
                {
                    sampled_tp_offsets.Add(sampled_all_offsets[i]);
                }
                else if (fp_offsets.Contains(sampled_all_offsets[i]))
                {
                    sampled_fp_offsets.Add(sampled_all_offsets[i]);
                }
            }
            List<List<long>> sampled_offsets = new List<List<long>>();
            sampled_offsets.Add(sampled_tp_offsets);
            sampled_offsets.Add(sampled_fp_offsets);
            return sampled_offsets;
        }

        private void Update_Sort_bvectors(List<long> tp_offsets, List<long> fp_offsets)
        {            
            // a block can appear twice in semant_effected_blocks: that would mean that a good token appears in it more than once
            Dictionary<int, double> semant_effected_blocks = Get_semantically_related_blocks(tp_offsets);            

            Dictionary<long, List<int>> file_obj_blocks_map = new Dictionary<long, List<int>>();
#if _FILE_SYSTEM_INFO
            file_obj_blocks_map = Get_offset_blocks_map_for_file_objects(tp_offsets);
#endif

            Add_Bad_Tokens_From_FP_offsets(fp_offsets);

            foreach (int block in semant_effected_blocks.Keys)
            {
                if (_bvectors.ContainsKey(block))
                {
                    Block_Vector bv = _bvectors[block];
                    bv.Update_Vector_Semantic(semant_effected_blocks[block]);
                }
            }           

            foreach(long offs in file_obj_blocks_map.Keys)
            {
                HashSet<int> adjusted_running_total_field_count_blocks = new HashSet<int>();
                for (int ind = 0; ind < file_obj_blocks_map[offs].Count; ind++)
                {
                    if (_bvectors.ContainsKey(file_obj_blocks_map[offs][ind]))
                    {
                        int block = file_obj_blocks_map[offs][ind];
                        Block_Vector bv = _bvectors[block];
                        if (!adjusted_running_total_field_count_blocks.Contains(block))
                        {
                            bv.Adjust_running_total_field_count();
                            adjusted_running_total_field_count_blocks.Add(block);
                        }
                        bv.Update_Vector_File_Object_info();
                    }
                }
            }           
            _bvectors = (from entry in _bvectors orderby entry.Value._net_score descending select entry).ToDictionary(pair => pair.Key, pair => pair.Value);
        }


        Dictionary<long, List<int>> Get_offset_blocks_map_for_file_objects(List<long> tp_offsets)
        {
            Dictionary<long, List<int>> offset_block_map = new Dictionary<long, List<int>>();

            for (int i = 0; i < tp_offsets.Count; i++)
            {
                long offset = tp_offsets[i];
                string file_name = _fso.Get_Filename(offset);
                if (file_name != null)
                {
                    List<int> related_blocks_for_file_obj = new List<int>(_fso._filename_block_map[file_name]);
                    if (!offset_block_map.ContainsKey(offset))
                    {
                        offset_block_map.Add(offset, related_blocks_for_file_obj);
                    }
                }
            }
            return offset_block_map;
        }

        Dictionary<int, double> Get_semantically_related_blocks(List<long> tp_offsets)
        {            
            Dictionary<int, double> semantic_block_update_scores = new Dictionary<int, double>();

            Dictionary<string, double> token_idfs = Get_Normalized_IDFs(tp_offsets);
            Dictionary<string, double> twin_token_idfs = Get_Twin_Token_Normalized_IDFs(token_idfs.Keys.ToList());
            Dictionary<string, double> combined_token_idfs = new Dictionary<string, double>(token_idfs);
            foreach (KeyValuePair<string, double> pair in twin_token_idfs)
            {
                if (!combined_token_idfs.ContainsKey(pair.Key))
                {
                    combined_token_idfs.Add(pair.Key, pair.Value);
                }
            }
            foreach (KeyValuePair<string, double> pair in combined_token_idfs)
            {
                string token = pair.Key;
                double idf = pair.Value;
                List<int> unique_blocks_for_token = _ifs._content_block_mapping[token].Distinct().ToList();
                if (!_good_accounted_tokens.ContainsKey(token) && !_bad_accounted_tokens.ContainsKey(token))
                {
                    for (int k = 0; k < unique_blocks_for_token.Count; k++)
                    {
                        int block = unique_blocks_for_token[k];
                        if (!semantic_block_update_scores.ContainsKey(block))
                        {
                            semantic_block_update_scores.Add(block, idf);
                        }
                        else
                        {
                            semantic_block_update_scores[block] += idf;
                        }
                    }                      
                    _good_accounted_tokens.Add(token, true);
                }
            }           
          
            return semantic_block_update_scores;
        }

        Dictionary<string, double> Get_Normalized_IDFs(List<long> tp_offsets)
        {
            Dictionary<string, double> token_idfs = new Dictionary<string, double>();

            for (int i = 0; i < tp_offsets.Count; i++)
            {
                long offset = tp_offsets[i];
                if (_ifs._offset_field_mapping.ContainsKey(offset))
                {
                    string[] dd = { "|" };
                    List<string> field_string_parts = new List<string>(_ifs._offset_field_mapping[offset].Split(dd, StringSplitOptions.RemoveEmptyEntries).ToList());
                    field_string_parts.RemoveAll(x => _bad_block_features.ContainsKey(x));
                    field_string_parts.RemoveAll(x => x.Length < 3);
                    field_string_parts.RemoveAll(x => x.Length == 3 && !_bse._dict_words.ContainsKey(x));
                    field_string_parts.RemoveAll(x => _bse.Is_Field_Small_Phone_Num(x) == true);
                    field_string_parts.RemoveAll(x => _bse.Is_Field_Long_Phone_Num(x) == true);
                    field_string_parts.RemoveAll(x => _bse._dict_words.ContainsKey(x));

                    for (int j = 0; j < field_string_parts.Count; j++)
                    {
                        string token = field_string_parts[j];
                        List<int> unique_blocks_for_token = _ifs._content_block_mapping[token].Distinct().ToList();
                        double idf_token = Math.Log(_bse._blocks.Count * 1.0 / unique_blocks_for_token.Count);
                        if (!token_idfs.ContainsKey(token))
                        {
                            token_idfs.Add(token, idf_token);
                        }                        
                    }
                }
            }
            // normalize IDFs
            if (token_idfs.Count > 0)
            {
                double max_idf = token_idfs.Values.Max();
                List<string> keys = token_idfs.Keys.ToList();
                for (int i = 0; i < keys.Count; i++)
                {
                    token_idfs[keys[i]] /= max_idf;
                }
            }
            return token_idfs;
        }

        Dictionary<string, double> Get_Twin_Token_Normalized_IDFs(List<string> orig_tokens)
        {
            /// for using the semantic relation between 98878... and 198878
            Dictionary<string, double> token_idfs = new Dictionary<string, double>();

            for (int i = 0; i < orig_tokens.Count; i++)
            {
                string token = orig_tokens[i];
                if (token.All(c => char.IsDigit(c)))
                {
                    if (token.Length == 10)
                    {
                        string twin_token = "1" + token;
                        if (_ifs._content_block_mapping.ContainsKey(twin_token))
                        {
                            List<int> blocks_for_twin = _ifs._content_block_mapping[twin_token].Distinct().ToList();
                            double idf_twin = Math.Log(_bse._blocks.Count * 1.0 / blocks_for_twin.Count);
                            if (!token_idfs.ContainsKey(twin_token))
                            {
                                token_idfs.Add(twin_token, idf_twin);
                            }
                        }
                    }
                    else if (token.Length == 11)
                    {
                        string twin_token = token.Substring(1);
                        if (_ifs._content_block_mapping.ContainsKey(twin_token))
                        {
                            List<int> blocks_for_twin = _ifs._content_block_mapping[twin_token].Distinct().ToList();
                            double idf_twin = Math.Log(_bse._blocks.Count * 1.0 / blocks_for_twin.Count);
                            if (!token_idfs.ContainsKey(twin_token))
                            {
                                token_idfs.Add(twin_token, idf_twin);
                            }
                        }
                    }
                }
            }
            // normalize IDFs
            if (token_idfs.Count > 0)
            {
                double max_idf = token_idfs.Values.Max();
                List<string> keys = token_idfs.Keys.ToList();
                for (int i = 0; i < keys.Count; i++)
                {
                    token_idfs[keys[i]] /= max_idf;
                }
            }
            return token_idfs;
        }

        void Add_Bad_Tokens_From_FP_offsets(List<long> fp_offsets)
        {
            for (int i = 0; i < fp_offsets.Count; i++)
            {
                long offset = fp_offsets[i];                

                if (_ifs._offset_field_mapping.ContainsKey(offset))
                {
                    string[] dd = { "|" };
                    List<string> field_string_parts = new List<string>(_ifs._offset_field_mapping[offset].Split(dd, StringSplitOptions.RemoveEmptyEntries).ToList());

                    for (int j = 0; j < field_string_parts.Count; j++)
                    {
                        if (!_bad_accounted_tokens.ContainsKey(field_string_parts[j]))
                        {                            
                            _bad_accounted_tokens.Add(field_string_parts[j], true);
                        }
                    }
                }
            }
        }     

        int Get_block_given_offset(long offset)
        {
            foreach (KeyValuePair<int, List<long>> pair in _block_offset_mapping_initial_sorted)
            {
                if (pair.Value.Contains(offset))
                {
                    return pair.Key;
                }
            }
            return -1;
        }

        #endregion

        #region Reading Files

        private void Load_Ground_Truth_Offsets()
        {
            StreamReader reader = new StreamReader(_input_CSV_path);           
            string[] dels2 = { "\t" };           
            string line = reader.ReadLine();
            while (line != "" && line != null)
            {
                if (!line.StartsWith("#"))
                {
                    string label = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[5];
                    if (label == "True")
                    {
                        long offset = long.Parse(line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[2]);
                        if (offset != 0)
                        {
                            _ground_truth_true_offsets.Add(offset);
                        }
                    }
                }
                line = reader.ReadLine();
            }
            reader.Close();
        }

        private void List_Good_Blocks()
        {
            foreach (KeyValuePair<int, List<long>> pair in _ifs._block_start_end)
            {
                int block = pair.Key;
                long block_beg = pair.Value[0];
                long block_end = pair.Value[1];
                for (int i = 0; i < _ground_truth_true_offsets.Count; i++)
                {
                    if (_ground_truth_true_offsets[i] >= block_beg && _ground_truth_true_offsets[i] <= block_end)
                    {
                        _TP_blocks.Add(block);
                        break;
                    }
                }
            }
        }

        private void Load_Block_Offset_Mapping()
        {
            _block_CSV_line_mapping = new Dictionary<int, List<string>>();
            StreamReader reader = new StreamReader(_input_CSV_path);
            string[] dels2 = { "\t" };
            string line = reader.ReadLine();
            while (line != null && line != "")
            {
                if (!line.StartsWith("#"))
                {
                    string field = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[1];

                    string[] parts = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries);
                    int block = int.Parse(parts[0]);
                    string field_line = string.Empty;
                    for (int ind = 1; ind < 6; ind++)
                    {
                        field_line += parts[ind] + "\t";
                    }

                    if (!_block_CSV_line_mapping.ContainsKey(block))
                    {
                        List<string> block_lines = new List<string>();
                        block_lines.Add(field_line);
                        _block_CSV_line_mapping.Add(block, block_lines);
                    }
                    else
                    {
                        _block_CSV_line_mapping[block].Add(field_line);
                    }
                }
                line = reader.ReadLine();
            }
            reader.Close();
            /// sort blocks using unsupervised/initial sorting algorithm 
            _sorted_blocks = new Dictionary<int, List<string>>();

#if _BASE_LINE
                _bse = new TextRanker(_block_CSV_line_mapping, _dictionary_path, _pre_pop_path);
                _baseline = _bse._baseline;
                _baseline.Rank_Blocks();               
                _sorted_blocks = _baseline._sorted_blocks;
                _block_scores = _baseline._block_scores;
#else
#if _FILE_SYSTEM_INFO
                _bse = new TextRanker(_block_CSV_line_mapping, _dictionary_path, _pre_pop_path, _offset_map_path);
                _fso = _bse._fso;

#else
#if _HYBRID
                _bse = new TextRanker(_block_CSV_line_mapping, _dictionary_path, _pre_pop_path, _ground_truth_token_file_path, _input_CSV_path, _offset_map_path);
                _fso = _bse._fso;
                _investigator_input = _bse._investigator_input;
#else
            _bse = new TextRanker(_block_CSV_line_mapping, _dictionary_path, _pre_pop_path, _ground_truth_token_file_path, _input_CSV_path);
            _investigator_input = _bse._investigator_input;
#endif
#endif
            _bse.Rank_Blocks();
            _bad_block_features = new Dictionary<string, double>(_bse._bad_block_features);

            _sorted_blocks = _bse._sorted_blocks;
            _block_scores = _bse._block_scores;
#endif
            //Write_Initial_Sorting_To_File(sorted_blocks);

            foreach (KeyValuePair<int, List<string>> pair in _sorted_blocks)
            {
                int curr_block = pair.Key;
                List<string> block_lines = pair.Value;
                for (int i = 0; i < block_lines.Count; i++)
                {
                    string curr_line = block_lines[i];
                    long Offset = long.Parse(curr_line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[1]);
                    string field = curr_line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[0];
                    if (Offset != 0 && (field.StartsWith("Text_") || field.StartsWith("PhoneNumber_")))
                    {
                        if (!_block_offset_mapping_initial_sorted.ContainsKey(curr_block))
                        {
                            List<long> Offset_list = new List<long>();
                            Offset_list.Add(Offset);
                            _block_offset_mapping_initial_sorted.Add(curr_block, Offset_list);
                        }
                        else
                        {
                            _block_offset_mapping_initial_sorted[curr_block].Add(Offset);
                        }
                    }
                }
            }
        } 

        #endregion

        #region Writing Files

        private void Write_Initial_Sorting_To_File(Dictionary<int, List<string>> sorted_blocks)
        {
            StringBuilder bldr = new StringBuilder(string.Empty);
            foreach (KeyValuePair<int, List<string>> pair in sorted_blocks)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    bldr.Append(pair.Key.ToString() + "\t" + pair.Value[i] + "\n");
                }
            }

            StreamWriter wr = new StreamWriter("initial_sorting.csv", false);
            wr.AutoFlush = true;
            wr.Write(bldr);
            wr.Close();
        }       

        private void Write_Remaining_Bvectors()
        {
            StreamWriter wr = new StreamWriter("remaining.csv");
            wr.AutoFlush = true;
            StringBuilder bldr = new StringBuilder(string.Empty);
            List<int> TP_blocks_list = _TP_blocks.ToList();
            for (int i = 0; i < TP_blocks_list.Count; i++)
            {
                if (_bvectors.ContainsKey(TP_blocks_list[i]))
                {
                    bldr.Append(TP_blocks_list[i].ToString() + " " + _bvectors[TP_blocks_list[i]]._file_object_score.ToString() + " " + _bvectors[TP_blocks_list[i]]._semantic_score.ToString()
                        + " " + _bvectors[TP_blocks_list[i]]._initial_sorting_score.ToString() + " " + _bvectors[TP_blocks_list[i]]._net_score.ToString() + "\n");
                }
            }               
            wr.Write(bldr);
            wr.Close();
        }

        private void Write_Rank_Vector(List<int> r_vector)
        {
            StringBuilder bldr = new StringBuilder(string.Empty);
            string ranks = string.Join(",", r_vector);
            _rank_vector_wr.WriteLine(ranks);
        }

        #endregion
    }

    class Block_Vector
    {
        public int _block;
        public double _initial_sorting_score;
        public double _semantic_score;
        public double _file_object_score;
        public double _net_score;

        private int _tokens_also_present_in_TP_fields;              
        private int _total_valid_block_tokens;
        private int _fields_part_of_TP_marked_files;
        private int _total_fields;
        private int _running_total_fields;

        public Block_Vector(int block, double text_score, int valid_block_tokens, int total_fields)
        {
            _block = block;
            _initial_sorting_score = text_score;
            _total_valid_block_tokens = valid_block_tokens;
            _tokens_also_present_in_TP_fields = 0;     
            _semantic_score = 0.0;
            _fields_part_of_TP_marked_files = 0;
            _file_object_score = 0.0;
            _total_fields = total_fields;
            _running_total_fields = 0;
            Update_Net_Score();
        }

        public void Update_Net_Score()
        {           
            _net_score = (0.50 * _initial_sorting_score) + (0.50 * _semantic_score);
        }

        public void Update_Vector_Semantic(double score)
        {
            _tokens_also_present_in_TP_fields++;
            _semantic_score += (1.0 * score);
            Update_Net_Score();
        }

        public void Update_Vector_File_Object_info()
        {            
            _fields_part_of_TP_marked_files++;
            _file_object_score = (_fields_part_of_TP_marked_files * 1.0) / (_running_total_fields + 1);
            Update_Net_Score();
        }

        public void Adjust_running_total_field_count()
        {
            _running_total_fields += _total_fields;
        }
    }
}

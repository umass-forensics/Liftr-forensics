using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace LiFTR
{
    public class TextRanker
    {
        private string _dict_path;
        private string _pre_pop_path;

        public Dictionary<int, List<string>> _blocks;
        public Dictionary<int, List<string>> _sorted_blocks;        
        public Dictionary<string, int> _dict_words;

        private string[] _chars_to_split_on;
        private string[] _bad_token_indicators;

        public Dictionary<string, double> _bad_block_features;
        public Dictionary<int, double> _block_scores;                     

        string _chunk_offset_filename_map_path;
        public FileSystemInfo _fso = null;

        string _ground_truth_token_file;
        string _inference_res_csv;
        public InvestigatorInput _investigator_input = null;

        public BaseLine _baseline = null;

        ///BASELINE: FIELD PAIRS
        public TextRanker(Dictionary<int, List<string>> block_CSV_line_mapping, string dict_path, string pre_pop_path)
        {
            _blocks = block_CSV_line_mapping;
            _dict_path = dict_path;
            _pre_pop_path = pre_pop_path;

            _dict_words = new Dictionary<string, int>();
            _bad_block_features = new Dictionary<string, double>();
            Read_dictionary();
            Read_Prepop_image();
            _baseline = new BaseLine(block_CSV_line_mapping);
        }

        ///FILE SYSTEM INFO
        public TextRanker(Dictionary<int, List<string>> block_CSV_line_mapping, string dict_path, string pre_pop_path, string offset_map_path)
        {
            _blocks = block_CSV_line_mapping;
            _sorted_blocks = new Dictionary<int, List<string>>();
            _dict_path = dict_path;
            _pre_pop_path = pre_pop_path;
          
            _dict_words = new Dictionary<string, int>();
            _bad_block_features = new Dictionary<string, double>();            
            Read_dictionary();
            Read_Prepop_image();            
            _chars_to_split_on = new string[] { "@", "!", "#", "$", "%", "&", "*", "(", ")", "-", "[", "]", ":", ";", "'", "?", ".", ",", " ", "\"" };
            _bad_token_indicators = new string[] { "+", "=", "`", "~", "<", ">", "{", "}", "*", "\\", "_", "^", "/", "|" };
            _block_scores = new Dictionary<int, double>();

            _chunk_offset_filename_map_path = offset_map_path;
            _fso = new FileSystemInfo(_chunk_offset_filename_map_path);
            _fso.Get_filename_block_map(_blocks);
        }

        ///APRIORI
        public TextRanker(Dictionary<int, List<string>> block_CSV_line_mapping, string dict_path, string pre_pop_path, string ground_truth_token_file, string inference_res_csv)
        {
            _blocks = block_CSV_line_mapping;
            _sorted_blocks = new Dictionary<int, List<string>>();
            _dict_path = dict_path;
            _pre_pop_path = pre_pop_path;

            _dict_words = new Dictionary<string, int>();
            _bad_block_features = new Dictionary<string, double>();
            Read_dictionary();
            Read_Prepop_image();
            _chars_to_split_on = new string[] { "@", "!", "#", "$", "%", "&", "*", "(", ")", "-", "[", "]", ":", ";", "'", "?", ".", ",", " ", "\"" };
            _bad_token_indicators = new string[] { "+", "=", "`", "~", "<", ">", "{", "}", "*", "\\", "_", "^", "/", "|" };
            _block_scores = new Dictionary<int, double>();

            _ground_truth_token_file = ground_truth_token_file;
            _inference_res_csv = inference_res_csv;
            _investigator_input = new InvestigatorInput(_inference_res_csv, _ground_truth_token_file);
        }

        /// HYBRID
        public TextRanker(Dictionary<int, List<string>> block_CSV_line_mapping, string dict_path, string pre_pop_path, string ground_truth_token_file, string inference_res_csv,
            string offset_map_path)
        {
            _blocks = block_CSV_line_mapping;
            _sorted_blocks = new Dictionary<int, List<string>>();
            _dict_path = dict_path;
            _pre_pop_path = pre_pop_path;

            _dict_words = new Dictionary<string, int>();
            _bad_block_features = new Dictionary<string, double>();
            Read_dictionary();
            Read_Prepop_image();
            _chars_to_split_on = new string[] { "@", "!", "#", "$", "%", "&", "*", "(", ")", "-", "[", "]", ":", ";", "'", "?", ".", ",", " ", "\"" };
            _bad_token_indicators = new string[] { "+", "=", "`", "~", "<", ">", "{", "}", "*", "\\", "_", "^", "/", "|" };
            _block_scores = new Dictionary<int, double>();

            _chunk_offset_filename_map_path = offset_map_path;
            _fso = new FileSystemInfo(_chunk_offset_filename_map_path);
            _fso.Get_filename_block_map(_blocks);

            _ground_truth_token_file = ground_truth_token_file;
            _inference_res_csv = inference_res_csv;
            _investigator_input = new InvestigatorInput(_inference_res_csv, _ground_truth_token_file);
        }

        #region Reading Files

        void Read_dictionary()
        {
            StreamReader reader = new StreamReader(_dict_path);
            string entire_file = reader.ReadToEnd();
            reader.Close();
            string[] dictionary_file_delims = { "\n", "\r" };
            string[] words = entire_file.Split(dictionary_file_delims, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (!_dict_words.ContainsKey(words[i]))
                {
                    _dict_words.Add(words[i].ToLower(), 1);
                }
            }
        }

        void Read_Prepop_image()
        {
            string[] all_special_chars = new string[] { "@", "!", "#", "$", "%", "&", "*", "(", ")", "-", "[", "]", ":", ";", "'", "?", ".", ",", " ", "\"",
             "+", "=", "`", "~", "<", ">", "{", "}", "*", "\\", "_", "^", "/", "|"};
            StreamReader rdr = new StreamReader(_pre_pop_path);
            string entire_file = rdr.ReadToEnd();
            rdr.Close();
            string[] dels1 = { "\n", "\r" };
            List<string> lines = entire_file.Split(dels1, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                string[] dels2 = { "\t" };
                string field_string = lines[i].Split(dels2, StringSplitOptions.RemoveEmptyEntries)[4].ToLower();

                string[] field_string_parts = field_string.Split(all_special_chars, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < field_string_parts.Length; j++)
                {
                    if (!_bad_block_features.ContainsKey(field_string_parts[j]))
                    {
                        _bad_block_features.Add(field_string_parts[j], 1);
                    }
                    else
                    {
                        _bad_block_features[field_string_parts[j]]++;
                    }
                }
            }           
        }     

        #endregion

        #region Verifier Methods
       
        bool Is_Correct_By_Syntax_Rules(string field_string)
        {
            if (Is_Bad_Character_Present(field_string) || Is_Camel_Text_Present(field_string) || !Is_non_codish_word_present(field_string)
                       /*|| Is_Number_Squeezed_in_alphabets(field_string)*/ || Is_Too_Long(field_string) || Is_Too_Short(field_string) /*||
                       (Is_Caps_Present_Except_Beginning(field_string)) || Is_Field_Small_Phone_Num(field_string)*/)
            {
                return false;
            }

            return true;
        }

        bool Is_Bad_Character_Present(string token)
        {
            for (int i = 0; i < token.Length; i++)
            {
                if (_bad_token_indicators.Contains(token[i].ToString()))
                {
                    return true;
                }
            }
            return false;
        }

        bool Is_Camel_Text_Present(string token)
        {
            List<string> parts = token.Split(_chars_to_split_on, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                string part = parts[i];
                for (int j = 1; j < part.Length; j++)
                {
                    if (part[j] >= 65 && part[j] <= 90 && part[j - 1] >= 97 && part[j - 1] <= 122)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        bool Is_Caps_Present_Except_Beginning(string token)
        {
            List<string> parts = token.Split(_chars_to_split_on, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = 1; j < parts[i].Length; j++)
                {
                    if (parts[i][j] >= 65 && parts[i][j] <= 90)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        bool Is_non_codish_word_present(string token)
        {
            List<string> parts = token.ToLower().Split(_chars_to_split_on, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                if (!_bad_block_features.ContainsKey(parts[i]))
                {
                    return true;
                }
            }
            return false;
        }

        bool Is_Too_Long(string token)
        {
            if (token.Length > 500)
            {
                return true;
            }
            List<string> parts = token.Split(_chars_to_split_on, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Length > 10)
                {
                    return true;
                }
            }
            return false;
        }

        bool Is_Too_Short(string token)
        {
            if (token.Length < 4)
            {
                return true;
            }
            List<string> parts = token.Split(_chars_to_split_on, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Length == 3 && !_dict_words.ContainsKey(parts[i]))
                {
                    return true;
                }
                if (parts[i].Length < 3)
                {
                    return true;
                }
            }
            return false;
        }

        public bool Is_Number_Squeezed_in_alphabets(string token)
        {
            List<string> parts = token.Split(_chars_to_split_on, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                string str = parts[i];

                for (int j = 1; j < str.Length - 1; j++)
                {
                    if ((str[j] >= 48 && str[j] <= 57) && ((str[j - 1] >= 65 && str[j - 1] <= 90) || (str[j - 1] >= 97 && str[j - 1] <= 122)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool Is_Field_Small_Phone_Num(string token)
        {
            for (int i = 0; i < token.Length; i++)
            {
                if (!(token[i] >= 48 && token[i] <= 57))
                {
                    return false;
                }
            }
            if (token.Length < 7)
            {
                return true;
            }
            return false;
        }

        public bool Is_Field_Long_Phone_Num(string token)
        {
            if (token.All(c => char.IsDigit(c)) && token.Length > 11)
            {
                return true;
            }
            return false;
        }

        #endregion

        public void Rank_Blocks()
        {
            foreach (KeyValuePair<int, List<string>> pair in _blocks)
            {
                int block_num = pair.Key;
                List<string> lines = pair.Value;
                int syntax_valid_text_field_ctr = 0;
                int bulk_extractor_valid_field_ctr = 0;
                int total_text_field_ctr = 0;
                int useful_file_offsets = 0;

                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i];
                    string[] dels = { "\t" };
                    string field = line.Split(dels, StringSplitOptions.RemoveEmptyEntries)[0];
                    string field_string = line.Split(dels, StringSplitOptions.RemoveEmptyEntries)[3];
                    long offset = long.Parse(line.Split(dels, StringSplitOptions.RemoveEmptyEntries)[1]);

                    bool is_useful_file = false;
                    if (_fso != null)
                    {
                        is_useful_file = _fso.Is_Useful_File(offset);
                    }
                    if (_investigator_input != null)
                    {
                        is_useful_file = is_useful_file || _investigator_input.Is_Useful_File(offset);
                    }

                    if (is_useful_file && (field.StartsWith("Text_") || field.StartsWith("PhoneNumber_"))) // not giving contacts2.db credit to timestamps
                    {
                        useful_file_offsets++;
                    }                   
                    
                    if (field.StartsWith("Text_"))
                    {
                        total_text_field_ctr++;

                        if (Is_Correct_By_Syntax_Rules(field_string) && is_useful_file)
                        {
                            syntax_valid_text_field_ctr++;
                        }
                        else
                        {
                            //syntax_valid_text_field_ctr--;
                        }                                             
                    }                    
                }
                double sc = ((syntax_valid_text_field_ctr * 0.4/*0.35*/) + (bulk_extractor_valid_field_ctr * 0.0) + (useful_file_offsets * 0.6/*0.65*/)) / (pair.Value.Count);
                _block_scores.Add(block_num, sc);
            }
            _block_scores = (from entry in _block_scores orderby entry.Value descending select entry).ToDictionary(x => x.Key, x => x.Value);
            _sorted_blocks = (from entry in _blocks orderby _block_scores[entry.Key] descending select entry).ToDictionary(x => x.Key, x => x.Value);
        }         
    }           
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Dec0de.Bll;
using Microsoft.Isam.Esent.Collections.Generic;

namespace LiFTR
{
    public class InvestigatorFeedbackSorting
    {
        private string _input_CSV_path;
        private string _input_field_paths_file_path;
        private StreamReader _input_CSV_reader;       
        public Dictionary<int, List<long>> _block_start_end;
        public int _total_blocks;

        public Dictionary<int, List<string>> _block_content_mapping;
        public Dictionary<string, List<int>> _content_block_mapping;

        public Dictionary<long, List<string>> _offset_fieldstring_mapping;
        //public PersistentDictionary<long, string> _offset_field_mapping;
        public Dictionary<long, string> _offset_field_mapping;

        private Dictionary<int,List<long>> _block_offset_map;
        private Dictionary<string, double> _bad_block_features;

        private Dictionary<string, int> _dict_words;

        public InvestigatorFeedbackSorting(string input_CSV_path, string field_paths_file_path, 
            Dictionary<int, List<long>> block_offset_map, Dictionary<string, double> bad_block_features)
        {
            _input_CSV_path = input_CSV_path;
            _input_CSV_reader = new StreamReader(_input_CSV_path);
            _input_field_paths_file_path = field_paths_file_path;                                
            _block_start_end = new Dictionary<int, List<long>>();            
            Load_Block_beg_end();
            _block_content_mapping = new Dictionary<int, List<string>>();
            _content_block_mapping = new Dictionary<string, List<int>>();
            Load_Content_Block_Mappings();
            _offset_fieldstring_mapping = new Dictionary<long, List<string>>();
            //_offset_field_mapping = new PersistentDictionary<long, string>("TMP");
            _offset_field_mapping = new Dictionary<long, string>();
            _block_offset_map = new Dictionary<int, List<long>>(block_offset_map);
            _bad_block_features = new Dictionary<string, double>(bad_block_features);
            Load_Offset_Fieldstring_mapping();
            _dict_words = new Dictionary<string, int>();
        }       

        private void Load_dict_words()
        {
            StreamReader reader = new StreamReader("dictionary.dic");
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

        private void Load_Block_beg_end()
        {
            StreamReader rdr = new StreamReader(_input_CSV_path);            
            string[] del1 = { "\n", "\r" };
            string[] del2 = { "\t" };           
            string field_line = rdr.ReadLine();
            while (field_line != "" && field_line != null)
            {
                //string field_line = lines[i];
                if (!field_line.StartsWith("#"))
                {
                    int block_num = Convert.ToInt32(field_line.Split(del2, StringSplitOptions.RemoveEmptyEntries)[0]);
                    long field_offset = long.Parse(field_line.Split(del2, StringSplitOptions.RemoveEmptyEntries)[2]);
                    if (!_block_start_end.ContainsKey(block_num))
                    {
                        List<long> start_end = new List<long>();
                        start_end.Add(field_offset); // the beginning offset of the block
                        start_end.Add(field_offset); // the ending offset of the block- will be updated when a line from same block reappears
                        _block_start_end.Add(block_num, start_end);
                    }
                    else
                    {
                        _block_start_end[block_num][1] = Math.Max(_block_start_end[block_num][1], field_offset); // updating the end offset of the block
                    }
                }
                field_line = rdr.ReadLine();
            }            
            _total_blocks = _block_start_end.Count;
        }                   

        private void Load_Offset_Fieldstring_mapping()
        {
            StreamReader rdr = new StreamReader(_input_CSV_path);           
            string[] dels = { "\n", "\r" };            
            string line = rdr.ReadLine();
            while(line!="" && line!=null)
            {                
                if (!line.StartsWith("#"))
                {
                    string[] dels2 = { "\t" };
                    string state = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[1];
                    if (state.StartsWith("Text_") || state.StartsWith("PhoneNumber_"))
                    {
                        long offset = long.Parse(line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[2]);
                        string[] dels3 = { "~", "`", "@","!", "#", "$", "%", "^", "&", "*", "(", ")", "_", "+", "=", "-", "{", "}", "[", "]", ":", ";", "\"", "'",
                                             "?", "/",".", "<", ">", ",", "|", "\\" ," "};
                        string content = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[4];
                        List<string> parts = content.Split(dels3, StringSplitOptions.RemoveEmptyEntries).ToList();
                        for (int j = 0; j < parts.Count; j++)
                        {
                            parts[j] = parts[j].ToLower();
                        }
                       
                        if (!_offset_field_mapping.ContainsKey(offset))
                        {
                            string x_string = string.Join("|", parts);
                            _offset_field_mapping.Add(offset, x_string);
                        }
                    }
                }
                line = rdr.ReadLine();
            }
        }

        private void Load_Content_Block_Mappings()
        {
            string[] dels = { "\n", "\r" };
            StreamReader rdr = new StreamReader(_input_CSV_path);            
            string line = rdr.ReadLine();
            while(line != "" && line != null)
            {                
                string[] dels2 = { "\t" };                
                if (!line.StartsWith("#"))
                {
                    int block = int.Parse(line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[0]);
                    string field = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[1];
                    string content = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[4];
                    if (field.StartsWith("Text_") || field.StartsWith("PhoneNumber_"))
                    {
                        string[] dels3 = { "~", "`", "@","!", "#", "$", "%", "^", "&", "*", "(", ")", "_", "+", "=", "-", "{", "}", "[", "]", ":", ";", "\"", "'",
                                             "?", "/",".", "<", ">", ",", "|", "\\" ," "};
                        List<string> parts = content.ToLower().Split(dels3, StringSplitOptions.RemoveEmptyEntries).ToList();                      
                        
                        for (int j = 0; j < parts.Count; j++)
                        {
                            string part = parts[j];
                            if (!_block_content_mapping.ContainsKey(block))
                            {
                                List<string> tmp = new List<string>();
                                tmp.Add(part);
                                _block_content_mapping.Add(block, tmp);
                            }
                            else
                            {
                                _block_content_mapping[block].Add(part);
                            }

                            if (!_content_block_mapping.ContainsKey(part))
                            {
                                List<int> b_list = new List<int>();
                                b_list.Add(block);
                                _content_block_mapping.Add(part, b_list);
                            }
                            else
                            {
                                _content_block_mapping[part].Add(block);
                            }
                        }
                    }
                }
                line = rdr.ReadLine();
            }
        }       
    }    
}

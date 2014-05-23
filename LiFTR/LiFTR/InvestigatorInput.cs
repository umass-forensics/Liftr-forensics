using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace LiFTR
{
    public class InvestigatorInput
    {
        public List<long> _ground_truth_offsets = null;
        public List<long> _sampled_tp_offsets = null;
        private HashSet<string> _ground_truth_strings = null;
        private string _inference_res_csv_path = null;
        private string _ground_truth_token_path = null;

        public InvestigatorInput(string inference_results_csv, string ground_truth_tokens)
        {
            _inference_res_csv_path = inference_results_csv;
            _ground_truth_token_path = ground_truth_tokens;
            _ground_truth_strings = new HashSet<string>();
            Load_Ground_Truth_Strings();
            _sampled_tp_offsets = new List<long>();
            _ground_truth_offsets = new List<long>();
            Sample_TP_offsets();
        }

        public bool Is_Useful_File(long field_offset)
        {
            if (_sampled_tp_offsets.Contains(field_offset))
            {
                return true;
            }
            return false;
        }

        private void Sample_TP_offsets()
        {
            StreamReader reader = new StreamReader(_inference_res_csv_path);
            string[] dels2 = { "\t" };
            string line = reader.ReadLine();
            Dictionary<string, List<long>> ground_truth_string_offset_map = new Dictionary<string, List<long>>();

            while (line != null && line != "")
            {
                if (!line.StartsWith("#"))
                {
                    string label = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[5];
                    string field_string = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[4];
                    long offset = long.Parse(line.Split(dels2, StringSplitOptions.RemoveEmptyEntries)[2]);

                    string[] spcl_chars = { "~", "`", "@","!", "#", "$", "%", "^", "&", "*", "(", ")", "_", "+", "=", "-", "{", "}", "[", "]", ":", ";", "\"", "'", "?", "/",
                                  ".", "<", ">", ",", "|", "\\" , " "};
                    string[] sub_parts = field_string.Split(spcl_chars, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string entry in sub_parts)
                    {
                        string val = entry.ToLower();
                        if (_ground_truth_strings.Contains(val))
                        {
                            if (!ground_truth_string_offset_map.ContainsKey(val))
                            {
                                List<long> offsets = new List<long>();
                                offsets.Add(offset);
                                ground_truth_string_offset_map.Add(val, offsets);
                            }
                            else
                            {
                                ground_truth_string_offset_map[val].Add(offset);
                            }
                        }
                    }
                }
                line = reader.ReadLine();
            }
            reader.Close();

            List<string> ground_truth_list = new List<string>(ground_truth_string_offset_map.Keys);
            List<string> final_gt_chosen = new List<string>();
            List<long> final_offset_chosen = new List<long>();
            if (ground_truth_list.Count > 5)
            {
                Random rand = new Random();
                int ctr = 0;
                while (ctr < 5)
                {
                    int ind1 = rand.Next(0, ground_truth_list.Count);
                    string gt_val = ground_truth_list[ind1];
                    if (!final_gt_chosen.Contains(gt_val))
                    {
                        final_gt_chosen.Add(gt_val);
                        List<long> gt_offsets = ground_truth_string_offset_map[gt_val];
                        //int ind2 = rand.Next(0, gt_offsets.Count);
                        //long offset = gt_offsets[ind2];
                        //_sampled_tp_offsets.Add(offset);
                        _sampled_tp_offsets.AddRange(gt_offsets);
                        ctr++;
                    }
                }
            }
            else
            {
                for (int i = 0; i < ground_truth_list.Count; i++)
                {
                    string gt_val = ground_truth_list[i];
                    final_gt_chosen.Add(gt_val);
                    List<long> gt_offsets = ground_truth_string_offset_map[gt_val];
                    _sampled_tp_offsets.AddRange(gt_offsets);
                }
            }
        }

        private void Load_Ground_Truth_Strings()
        {
            StreamReader rdr = new StreamReader(_ground_truth_token_path);
            string entire_file = rdr.ReadToEnd().ToLower();
            rdr.Close();
            string[] dels = { "\n", "\r" };
            List<string> tmp = entire_file.Split(dels, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (int i = 0; i < tmp.Count; i++)
            {
                _ground_truth_strings.Add(tmp[i]);
            }
        }
    }
}

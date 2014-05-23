using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace LiFTR
{
    public class BaseLine
    {
        public Dictionary<int, List<string>> _blocks = null;
        public Dictionary<int, List<string>> _sorted_blocks = null;
        public Dictionary<int, double> _block_scores = null;

        public BaseLine(Dictionary<int, List<string>> block_CSV_line_mapping)
        {
            _blocks = block_CSV_line_mapping;
            _block_scores = new Dictionary<int, double>();
        }

        public void Rank_Blocks()
        {
            foreach (KeyValuePair<int, List<string>> pair in _blocks)
            {
                int block = pair.Key;
                List<string> lines = new List<string>(pair.Value);
                string[] dels = { "\t" };

                //group multiple texts to one entry
                string pfield = "";
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i];
                    string curr_field = line.Split(dels, StringSplitOptions.RemoveEmptyEntries)[0];
                    if (pfield.StartsWith("Text_") && curr_field.StartsWith("Text_"))
                    {
                        lines.RemoveAt(i);
                        i--;
                    }
                    pfield = curr_field;
                }

                string prev_field = "";
                double score = 0.0;

                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i];
                    string curr_field = line.Split(dels, StringSplitOptions.RemoveEmptyEntries)[0];
                    if (prev_field.StartsWith("Text_") && curr_field.StartsWith("PhoneNumber_") ||
                        prev_field.StartsWith("PhoneNumber_") && curr_field.StartsWith("Text"))
                    {
                        score += 1.0;
                        prev_field = "";
                    }
                    else
                    {
                        prev_field = curr_field;
                    }
                }
                _block_scores.Add(block, score);
            }
            _sorted_blocks = (from entry in _blocks orderby _block_scores[entry.Key] descending select entry).ToDictionary(x => x.Key, x => x.Value);
        }
    }
}

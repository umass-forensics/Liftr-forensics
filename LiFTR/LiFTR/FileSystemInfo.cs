using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace LiFTR
{
    public class FileSystemInfo
    {
        public Dictionary<long, string> _chunk_offset_file_map = null;
        public Dictionary<string, List<int>> _filename_block_map = null;
        public List<string> _useful_files = null;

        public FileSystemInfo(string offset_filename_map_path)
        {
            _useful_files = new List<string>() { "contacts2.db", /*"contacts.db", "mmssms.db", "fb.db"*/ "mmssms.db" };
            _chunk_offset_file_map = new Dictionary<long, string>();
            Load_offset_filename_map(offset_filename_map_path);
        }

        void Load_offset_filename_map(string path)
        {
            StreamReader rdr = new StreamReader(path);
            string entire_file = rdr.ReadToEnd();
            rdr.Close();
            string[] dels = { "\n" };
            string[] lines = entire_file.Split(dels, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] dels2 = { "," };
                string[] parts = line.Split(dels2, StringSplitOptions.RemoveEmptyEntries);
                string file_name = parts[0];
                long offset = long.Parse(parts[2]);
                _chunk_offset_file_map.Add(offset, file_name);
            }
        }

        public void Get_filename_block_map(Dictionary<int, List<string>> blocks)
        {
            _filename_block_map = new Dictionary<string, List<int>>();
            foreach (KeyValuePair<int, List<string>> pair in blocks)
            {
                int block = pair.Key;
                List<long> offsets = new List<long>();
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    string line = pair.Value[i];
                    string[] dels = { "\t" };
                    string[] parts = line.Split(dels, StringSplitOptions.RemoveEmptyEntries);
                    long offset = long.Parse(parts[1]);
                    if (offset > 0)
                    {
                        offsets.Add(offset);
                    }
                }
                for (int i = 0; i < offsets.Count; i++)
                {
                    long chunk_start_offset = Get_chunk_start_offset(offsets[i]);
                    if (chunk_start_offset != -1)
                    {
                        string file_name = _chunk_offset_file_map[chunk_start_offset];
                        if (!_filename_block_map.ContainsKey(file_name))
                        {
                            List<int> block_for_file = new List<int>();
                            block_for_file.Add(block);
                            _filename_block_map.Add(file_name, block_for_file);
                        }
                        else
                        {
                            _filename_block_map[file_name].Add(block);
                        }
                    }
                }
            }
        }

        public long Get_chunk_start_offset(long field_offset)
        {
            long chunk_start = (field_offset / 2048) * 2048;
            if (_chunk_offset_file_map.ContainsKey(chunk_start))
            {
                return chunk_start;
            }
            return -1;
        }

        public bool Is_Useful_File(long field_offset)
        {
            string filename = Get_Filename(field_offset);
            if (_useful_files.Contains(filename))
            {
                return true;
            }
            return false;
        }

        public string Get_Filename(long field_offset)
        {
            long chunk_start = Get_chunk_start_offset(field_offset);
            if (chunk_start != -1)
            {
                string filename = _chunk_offset_file_map[chunk_start];
                return filename;
            }
            return null;
        }        
    }
}

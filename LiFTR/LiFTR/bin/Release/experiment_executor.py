__author__ = 'Saksham'

import os
import argparse
import subprocess


def main():
    parser = argparse.ArgumentParser(
        description="Does multiple runs for the sorting trace-driven experiments")
    parser.add_argument("--runs", help="The number of experiment runs for a phone", type=int, dest="num_runs")
    parser.add_argument("--exec", help="The path to the experiment executable", type=str, dest="executable_path")
    parser.add_argument("--csv", help="The path to the inference csv", type=str, dest="csv_path")
    parser.add_argument("--tokens", help="The path to the ground truth tokens", type=str, dest="tokens_path")
    parser.add_argument("--dict", help="The path to the dictionary file", type=str, dest="dict_path")
    parser.add_argument("--prepop", help="The path to the pre-population inference results", type=str,
                        dest="prepop_path")
    parser.add_argument("--map", help="The path to the offset map", type=str, dest="offset_map_path", default=None)
    args = parser.parse_args()

    for i in range(0, args.num_runs):
        run_id = i + 1
        root, ext = os.path.splitext(args.csv_path)
        accuracy_nums_path = root + "_accuracy_nums_" + str(run_id) + ".csv"
        rank_writer_path = root + "_ranks_" + str(run_id) + ".csv"
        if args.offset_map_path is None:
            subprocess.call(
                args.executable_path + " " + args.csv_path + " " + args.tokens_path + " " + args.dict_path + " " +
                args.prepop_path + " " + accuracy_nums_path + " " + rank_writer_path)
        else:
            subprocess.call(
                args.executable_path + " " + args.csv_path + " " + args.tokens_path + " " + args.dict_path + " " +
                args.prepop_path + " " + accuracy_nums_path + " " + rank_writer_path + " " + args.offset_map_path)
        print "Done %d runs . . ." % run_id
        pass
    pass


if __name__ == '__main__':
    main()


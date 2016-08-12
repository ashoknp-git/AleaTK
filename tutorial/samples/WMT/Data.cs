using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using ICSharpCode.SharpZipLib.Tar;

namespace Tutorial.Samples
{
    public class Data
    {
        public static string Name(string name)
        {
            return Path.Combine("Data", "Wmt15", name);
        }

        private static void Decompress(string src, string dst)
        {
            using (var originalFileStream = File.OpenRead(src))
            using (var decompressedFileStream = File.Create(dst))
            using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
            {
                decompressionStream.CopyTo(decompressedFileStream);
            }
        }

        private static void Extract(string src, string dst)
        {
            using (var tarFile = File.OpenRead(src))
            using (var tarArchive = TarArchive.CreateInputTarArchive(tarFile))
            {
                tarArchive.ExtractContents(dst);
            }
        }

        public static void EnsureDataFile()
        {
            const string doneFileName = @"Data\Wmt15.done";
            const string urlTrain = @"http://www.statmt.org/wmt10/training-giga-fren.tar";
            const string urlDev = @"http://www.statmt.org/wmt15/dev-v2.tgz";

            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }

            if (!File.Exists(doneFileName))
            {
                using (var client = new WebClient())
                {
                    if (!File.Exists(Name("training-giga-fren.tar")))
                    {
                        Console.WriteLine($"Downloading {urlTrain} ...");
                        client.DownloadFile(urlTrain, Name("training-giga-fren.tar"));
                    }
                    if (!File.Exists(Name("dev-v2.tgz")))
                    {
                        Console.WriteLine($"Downloading {urlDev} ...");
                        client.DownloadFile(urlDev, Name("dev-v2.tgz"));
                    }
                }

                Console.WriteLine($"Decompressing files ...");
                Decompress(Name("dev-v2.tgz"), Name("dev-v2.tar"));
                Extract(Name("dev-v2.tar"), Name("dev-v2"));
                Extract(Name("training-giga-fren.tar"), Name("training-giga-fren"));
                Decompress(Name(Path.Combine("training-giga-fren", "giga-fren.release2.en.gz")), Name(Path.Combine("training-giga-fren", "giga-fren.release2.en")));
                Decompress(Name(Path.Combine("training-giga-fren", "giga-fren.release2.fr.gz")), Name(Path.Combine("training-giga-fren", "giga-fren.release2.fr")));

                using (var doneFile = File.CreateText(doneFileName))
                {
                    doneFile.WriteLine($"{DateTime.Now}");
                }
            }
        }

        public static Tuple<Vocabulary, Dictionary<string, int>> CreateVocabulary(string filename, int maxVocabularySize, bool normalizeDigits = true, int print = 0)
        {
            var wordHistogram = new Dictionary<string, int>();
            using (var file = new StreamReader(filename, Encoding.UTF8, true))
            {
                string sentence;
                var counter = 0;
                while ((sentence = file.ReadLine()) != null)
                {
                    counter++;
                    if (counter%100000 == 0) Console.WriteLine($"CreateVocabulary from {filename} : line {counter}");
                    
                    var tokens = Vocabulary.Tokenizer(sentence);

                    foreach (var token in tokens)
                    {
                        var tok = normalizeDigits ? Vocabulary.NormalizeDigits(token) : token;

                        if (counter < print) Console.Write($"{tok}, ");

                        if (string.IsNullOrEmpty(tok)) continue;

                        if (wordHistogram.ContainsKey(tok))
                        {
                            wordHistogram[tok] += 1;
                        }
                        else
                        {
                            wordHistogram[tok] = 1;
                        }
                    }

                    if (counter < print) Console.WriteLine();
                }
            }

            var orderedWordHistogram = wordHistogram.OrderByDescending(kv => kv.Value).ToDictionary(x => x.Key, x => x.Value);

            Console.WriteLine($"{filename} : total number of words {orderedWordHistogram.Count}, building vocabulary with {maxVocabularySize} words");
            return new Tuple<Vocabulary, Dictionary<string, int>>(new Vocabulary(orderedWordHistogram, maxVocabularySize), orderedWordHistogram);
        }

        public static void TextToTokenIds(string filename, string tokenizedFilename, Vocabulary vocabulary, bool normalizeDigits = true)
        {
            using (var reader = new StreamReader(filename, Encoding.UTF8, true))
            using (var writer = new StreamWriter(tokenizedFilename))
            {
                string sentence;
                var counter = 0;
                while ((sentence = reader.ReadLine()) != null)
                {
                    counter++;
                    if (counter % 100000 == 0) Console.WriteLine($"TextToTokenIds of {filename} : line {counter}");

                    var tokenIds = vocabulary.SentenceToTokenIds(sentence, normalizeDigits);
                    var line = string.Join(" ", tokenIds.Select(i => i.ToString()));
                    writer.WriteLine(line);
                }
            }           
        }

        public static List<int[]> ReadTokenized(string filename)
        {
            var tokenIds = new List<int[]>();
            using (var reader = new StreamReader(filename, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var ids = line.Trim().Split(null).Select(int.Parse).ToArray();
                    tokenIds.Add(ids);
                }
            }
            return tokenIds;
        }

        public static string[] TokenIdsToText(int[] sentence, Vocabulary vocabulary)
        {
            return sentence.Select(tokenId => vocabulary.Words[tokenId]).ToArray();
        }

        public static List<string[]> TokenIdsToText(List<int[]> sentences, Vocabulary vocabulary)
        {
            return sentences.Select(sentence => TokenIdsToText(sentence, vocabulary).ToArray()).ToList();
        }

        public static List<int[]> FindSentenceContainingTokenId(List<int[]> sentences, int tokenId, int maxNum = 1)
        {
            var sentencesContainingToken = new List<int[]>();
 
            var i = 0;
            var counter = 0;
            foreach (var sentence in sentences)
            {
                counter++;
                if (counter % 1000 == 0) Console.WriteLine($"FindSentenceContainingTokenId : searched in {counter} lines ");

                if (Array.IndexOf(sentence, tokenId) >= 0)
                {
                    sentencesContainingToken.Add(sentence);
                    i++;
                }

                if (i >= maxNum) break;
            }
            return sentencesContainingToken;
        }

        public static void Display(string filename1, string filename2, int count)
        {
            using (var file1 = new StreamReader(filename1, Encoding.UTF8, true))
            using (var file2 = new StreamReader(filename2, Encoding.UTF8, true))
            {
                var i = 0;
                string line1, line2;
                while ((line1 = file1.ReadLine()) != null && (line2 = file2.ReadLine()) != null && i < count)
                {
                    Console.WriteLine($"{line1}");
                    Console.WriteLine($"{line2}");
                    Console.WriteLine();
                    i++;
                }
            }
        }

        public static void Preprocess(string trainingDataFilename, string testDataFilename, string trainingTokenizedFilename, string testTokenizedFilename, 
            string vocabularyFilename, int maxVocabularySize = 50000, bool normalizeDigits = true)
        {
            var vocabulary = CreateVocabulary(trainingDataFilename, maxVocabularySize, normalizeDigits);
            vocabulary.Item1.Save(vocabularyFilename);
            TextToTokenIds(trainingDataFilename, trainingTokenizedFilename, vocabulary.Item1);
            TextToTokenIds(testDataFilename, trainingTokenizedFilename, vocabulary.Item1, normalizeDigits);
        }

        /// <summary>
        /// Tokenized sequences are read from the source and target language file. The sequences are 
        /// distribute into different bucketSequenceLengths according to their sequence length. 
        /// By default the data is padded to the bucket length and the target sequence is prepended with the go symbol id.
        /// </summary>
        /// <param name="sourceLanguage"></param>
        /// <param name="targetLanguage"></param>
        /// <param name="bucketSequenceLengths"></param>
        /// <returns></returns>
        public static BucketedData BucketTokenizedData(string sourceLanguage, string targetLanguage, IEnumerable<Tuple<int, int>> bucketSequenceLengths)
        {
            var bucketedData = new BucketedData(bucketSequenceLengths);

            using (var file1 = new StreamReader(sourceLanguage, Encoding.UTF8, true))
            using (var file2 = new StreamReader(targetLanguage, Encoding.UTF8, true))
            {
                var counter = 0;
                string line1, line2;
                while ((line1 = file1.ReadLine()) != null && (line2 = file2.ReadLine()) != null)
                {
                    var source = line1.Trim().Split(null).Select(int.Parse).ToArray();
                    var target = line2.Trim().Split(null).Select(int.Parse).ToArray();

                    counter++;
                    if (counter%100000 == 0)
                        Console.WriteLine($"PrepareForTraining {sourceLanguage} {targetLanguage} : line {counter}");

                    bucketedData.Add(source, target);
                }
            }
            return bucketedData;
        }
    }
}
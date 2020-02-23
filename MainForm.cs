using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Text;
using System.Windows.Forms;

namespace Voicer
{
    public partial class MainForm : Form
    {
        private const double FrameDuration = 41.66666666666667;

        public MainForm()
        {
            InitializeComponent();
        }

        private static string GetVoiceName(Speak speak)
        {
            switch (speak.voice.voiceName)
            {
                case "maria":
                    return "Microsoft Maria Desktop";

                default:
                    return "Microsoft Daniel Desktop";
            }
        }

        private void AddLipSyncItem(Phoneme phoneme, Dictionary<uint, string> items)
        {
            var frame = (uint)(phoneme.position.TotalMilliseconds / FrameDuration);
            var mouth = GetMouth(phoneme.phoneme);

            if (!string.IsNullOrEmpty(mouth))
            {
                items[frame] = mouth;
                frame = (uint)((phoneme.position.TotalMilliseconds + phoneme.duration.TotalMilliseconds) / FrameDuration);
                items[frame] = "rest";
            }
        }

        private void AddSpeak(List<Speak> speaks, string line, IEnumerable<Voice> voices)
        {
            var parts = line.Split(':');

            if (parts.Length != 2)
            {
                throw new Exception("Invalid speak: " + line);
            }

            var speak = new Speak
            {
                character = parts[0]?.Trim(),
                text = parts[1].Trim()
            };

            speak.voice = voices.FirstOrDefault(x => x.character == speak.character) ?? voices.First();
            speaks.Add(speak);
        }

        private void AddVoice(List<Voice> voices, string line)
        {
            var parts = line.Split(',');

            if (parts.Length < 3)
            {
                throw new Exception("Invalid voice: " + line);
            }

            var voice = new Voice
            {
                character = parts[0].Trim(),
                voiceName = parts[1].Trim(),
                rate = sbyte.Parse(parts[2].Trim()),
            };

            voices.Add(voice);
        }

        private void export()
        {
            if (string.IsNullOrWhiteSpace(text.Text))
            {
                throw new Exception("Invalid text.");
            }

            if (!folderBrowserDialog.ShowDialog().Equals(DialogResult.OK))
            {
                return;
            }

            var textContent = text.Text?.ToLower();
            var lines = textContent.Split(Environment.NewLine.ToCharArray()).Where(x => !string.IsNullOrWhiteSpace(x?.Trim()));
            var voicesLines = lines.Where(x => x?.StartsWith("voice:") ?? false);
            var voices = GetVoices(voicesLines);
            var speaksLines = lines.Where(x => !x?.StartsWith("voice:") ?? false);
            var speaks = GetSpeaks(speaksLines, voices);

            for (int i = 0; i < speaks.Count(); i++)
            {
                var speak = speaks.ToArray()[i];
                export(speak, i);
            }
        }

        private void export(Speak speak, int index)
        {
            var file = string.Format("{0}\\speak_{1}_{2}.wav",
                folderBrowserDialog.SelectedPath,
                index.ToString("d3"),
                speak.character);
            speak.waveFile = file;
            var phonemes = new List<Phoneme>();
            Synthesize(speak, phonemes);
            speak.phonemes = phonemes.ToArray();
            SaveLipSync(speak);
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                Refresh();
                export();
            }
            catch (Exception ex)
            {
                UseWaitCursor = false;
                Refresh();
                MessageBox.Show(ex.Message, "Error");
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private string GetLipSync(Phoneme[] phonemes)
        {
            var items = new Dictionary<uint, string>
            {
                [0] = "rest"
            };

            foreach (var phoneme in phonemes)
            {
                AddLipSyncItem(phoneme, items);
            }

            var content = new StringBuilder();

            foreach (var item in items)
            {
                var line = string.Format("{0} {1}", item.Key, item.Value);
                content.AppendLine(line);
            }

            return content.ToString();
        }

        private string GetMouth(string phoneme)
        {
            switch (phoneme)
            {
                case "a":
                case "i":
                    return "ai";

                case "e":
                    return "e";

                case "o":
                    return "o";

                case "u":
                    return "u";

                case "f":
                case "v":
                    return "fv";

                case "l":
                    return "l";

                case "m":
                case "b":
                case "p":
                    return "mbp";

                case "w":
                case "q":
                    return "wq";

                default:
                    return null;
            }
        }

        private IEnumerable<Speak> GetSpeaks(IEnumerable<string> speaksLines, IEnumerable<Voice> voices)
        {
            var speaks = new List<Speak>();

            foreach (var line in speaksLines)
            {
                AddSpeak(speaks, line, voices);
            }

            return speaks;
        }

        private IEnumerable<Voice> GetVoices(IEnumerable<string> voicesLines)
        {
            var voices = new List<Voice>();
            var defaultVoice = new Voice
            {
                character = "default",
                voiceName = "daniel"
            };

            voices.Add(defaultVoice);

            foreach (var line in voicesLines)
            {
                AddVoice(voices, line);
            }

            return voices;
        }

        private void PhonemeReached(PhonemeReachedEventArgs args, List<Phoneme> phonemes)
        {
            var phoneme = new Phoneme
            {
                position = args.AudioPosition,
                phoneme = args.Phoneme,
                duration = args.Duration,
            };

            phonemes.Add(phoneme);
        }

        private void SaveLipSync(Speak speak)
        {
            var lipSync = GetLipSync(speak.phonemes);
            var file = string.Format("{0}\\{1}.dat",
                Path.GetDirectoryName(speak.waveFile),
                Path.GetFileNameWithoutExtension(speak.waveFile));
            File.WriteAllText(file, lipSync);
        }

        private void Synthesize(Speak speak, List<Phoneme> phonemes)
        {
            var format = new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null);
            var synth = new SpeechSynthesizer { Rate = speak.voice.rate };
            synth.SelectVoice(GetVoiceName(speak));
            synth.SetOutputToWaveFile(speak.waveFile, format);
            synth.PhonemeReached += (s, e) => PhonemeReached(e, phonemes);
            synth.Speak(speak.text);
            synth.Dispose();
        }
    }

    internal class Phoneme
    {
        public TimeSpan duration;
        public string phoneme;
        public TimeSpan position;
    }

    internal class Speak
    {
        public string character;
        public Phoneme[] phonemes;
        public string text;
        public Voice voice;
        public string waveFile;
    }

    internal class Voice
    {
        public string character;
        public sbyte rate;
        public string voiceName;
    }
}
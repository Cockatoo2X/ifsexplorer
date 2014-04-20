﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace IFSExplorer
{
    public partial class MainForm : Form
    {
        private static readonly Dictionary<int, int> InitialIndexGuesses =
            new Dictionary<int, int> {
                {257040, 50},
                {257400, 31},
                {282240, 58},
                {293760, 50},
                {310800, 38},
                {440640, 48},
                {674240, 23},
                {756576, 20},
                {820800, 63},
                {836752, 11},
                {872640, 42},
                {906096, 10}
            };

        private readonly Dictionary<int, int> _indexGuesses = new Dictionary<int, int>();
        private string _indexGuessesFilename;
        private Stream _currentStream;
        private FileIndex _currentFileIndex;
        private DecodedRaw _currentRaw;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            var path = Assembly.GetEntryAssembly().Location;
            var directoryName = Path.GetDirectoryName(path);
            if (directoryName == null) {
                FillInitialGuesses();
                return;
            }

            _indexGuessesFilename = Path.Combine(directoryName, "index_guesses.txt");
            if (!File.Exists(_indexGuessesFilename)) {
                FillInitialGuesses();
                return;
            }

            var lines = File.ReadAllLines(_indexGuessesFilename);
            foreach (var line in lines) {
                var split = line.Replace(" ", "").Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length != 2) {
                    continue;
                }

                int size, index;
                if (int.TryParse(split[0], out size) && int.TryParse(split[1], out index)) {
                    _indexGuesses.Add(size, index);
                }
            }
        }

        private void FillInitialGuesses()
        {
            foreach (var initialIndexGuess in InitialIndexGuesses) {
                _indexGuesses.Add(initialIndexGuess.Key, initialIndexGuess.Value);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            using (var fileStream = File.OpenWrite(_indexGuessesFilename))
            using (var streamWriter = new StreamWriter(fileStream)) {
                foreach (var indexGuess in _indexGuesses) {
                    streamWriter.WriteLine("{0}, {1}", indexGuess.Key, indexGuess.Value);
                }
            }
        }

        private void browseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog.InitialDirectory = @"C:\LDJ\data\graphic";
            var dialogResult = openFileDialog.ShowDialog();

            if (dialogResult != DialogResult.OK) {
                return;
            }

            listboxImages.Items.Clear();

            if (_currentStream != null) {
                _currentStream.Close();
            }

            _currentStream = openFileDialog.OpenFile();
            var mappings = IFSUtils.ParseIFS(_currentStream);

            foreach (var mapping in mappings) {
                listboxImages.Items.Add(new ImageItem(mapping));
            }
        }

        private void saveSelectedImageToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void listboxImages_SelectedIndexChanged(object sender, EventArgs e)
        {
            updownIndexSelect.Minimum = 0;
            updownIndexSelect.Value = 0;
            updownIndexSelect.Maximum = 0;
            updownIndexSelect.Enabled = true;
            pictureboxPreview.Refresh();
        }

        private void pictureboxPreview_Paint(object sender, PaintEventArgs e)
        {
            var imageItem = (ImageItem) listboxImages.SelectedItem;

            if (imageItem != null) {
                DrawFileIndex(e.Graphics, imageItem.FileIndex);
            }
        }

        private void updownIndexSelect_ValueChanged(object sender, EventArgs e)
        {
            pictureboxPreview.Refresh();
        }

        private void DrawFileIndex(Graphics graphics, FileIndex fileIndex)
        {
            if (_currentFileIndex != fileIndex) {
                _currentFileIndex = fileIndex;
                var rawBytes = IFSUtils.DecompressLSZZ(fileIndex.Read());

                try {
                    _currentRaw = IFSUtils.DecodeRaw(rawBytes);
                } catch (Exception e) {
                    _currentRaw = null;
                    labelStatus.Text = string.Format("Couldn't decode raw #{0}: {1}", fileIndex.EntryNumber, e);
                    return;
                }
            }

            if (_currentRaw == null) {
                return;
            }

            if (updownIndexSelect.Value == 0) {
                updownIndexSelect.Maximum = _currentRaw.IndexSize - 1;

                int indexGuess;
                if (!_indexGuesses.TryGetValue(_currentRaw.RawLength, out indexGuess)) {
                    indexGuess = (int) (((decimal) _currentRaw.IndexSize)/2);
                }
                updownIndexSelect.Value = indexGuess;
            }

            var index = (int) updownIndexSelect.Value;
            _indexGuesses[_currentRaw.RawLength] = index;

            var size = _currentRaw.GetSize(index);

            labelStatus.Text = string.Format("#{0}: {1} bytes decompresses to {2} bytes (index {3} = {4}x{5})",
                                             fileIndex.EntryNumber,
                                             fileIndex.Size, _currentRaw.RawLength, index, size.Item1, size.Item2);

            var colors = new Dictionary<int, SolidBrush>();

            try {
                for (var y = 0; y < size.Item2; ++y) {
                    for (var x = 0; x < size.Item1; ++x) {
                        var argb = _currentRaw.GetARGB(index, x, y);
                        var color = Color.FromArgb(argb);

                        SolidBrush brush;
                        if (!colors.TryGetValue(argb, out brush)) {
                            brush = new SolidBrush(color);
                            colors[argb] = brush;
                        }
                        graphics.FillRectangle(brush, x, y, 1, 1);
                    }
                }
            } finally {
                foreach (var pair in colors) {
                    pair.Value.Dispose();
                }
            }
        }

        private class ImageItem
        {
            internal readonly FileIndex FileIndex;

            internal ImageItem(FileIndex fileIndex)
            {
                FileIndex = fileIndex;
            }

            public override string ToString()
            {
                return string.Format("#{0} ({1})", FileIndex.EntryNumber, FileIndex.Size);
            }

        }
    }
}
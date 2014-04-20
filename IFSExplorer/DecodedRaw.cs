﻿using System;
using System.Drawing;

namespace IFSExplorer
{
    internal class DecodedRaw
    {
        internal readonly int RawLength;
        private readonly int _offset;
        private readonly int[] _argbArr;
        private readonly int[] _widths;
        private readonly int[] _heights;

        internal int IndexSize { get { return _widths.Length; } }

        internal DecodedRaw(int rawLength, int offset, int[] argbArr, int[] widths, int[] heights)
        {
            RawLength = rawLength;
            _heights = heights;
            _widths = widths;
            _argbArr = argbArr;
            _offset = offset;
        }

        internal Point GetSize(int index)
        {
            return new Point(_widths[index], _heights[index]);
        }

        internal int GetARGB(int index, int x, int y)
        {
            return _argbArr[(y*_widths[index]) + x + _offset];
        }
    }
}
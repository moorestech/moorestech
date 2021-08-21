using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace industrialization.Server.Util
{
    public class ByteArrayEnumerator
    {
        private readonly IEnumerator<byte> _payload;
        public ByteArrayEnumerator(byte[] payload)
        {
            _payload = payload.ToList().GetEnumerator();
        }

        public int MoveNextToGetInt()
        {
            var b = new List<byte>();
            for (int i = 0; i < 4; i++)
            {
                if (_payload.MoveNext())
                {
                    b.Add(_payload.Current);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("パケットフォーマットの解析に不具合があります");
                }
            }
            return BitConverter.ToInt32(b.ToArray(),0);
        }
        public uint MoveNextToGetUint()
        {
            var b = new List<byte>();
            for (int i = 0; i < 4; i++)
            {
                if (_payload.MoveNext())
                {
                    b.Add(_payload.Current);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("パケットフォーマットの解析に不具合があります");
                }
            }
            return BitConverter.ToUInt32(b.ToArray(),0);
        }

        public short MoveNextToGetShort()
        {
            var b = new List<byte>();
            for (int i = 0; i < 2; i++)
            {
                if (_payload.MoveNext())
                {
                    b.Add(_payload.Current);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("パケットフォーマットの解析に不具合があります");
                }
            }
            return BitConverter.ToInt16(b.ToArray(),0);
        }
        public float MoveNextToGetFloat()
        {
            var b = new List<byte>();
            for (int i = 0; i < 4; i++)
            {
                if (_payload.MoveNext())
                {
                    b.Add(_payload.Current);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("パケットフォーマットの解析に不具合があります");
                }
            }
            return BitConverter.ToSingle(b.ToArray(),0);
        }
        /// <summary>
        /// バイト数を指定してそのバイト数の文字列を取得します
        /// </summary>
        /// <param name="byteNum">バイト数 指定しないor0の時最後まで取得する</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public string MoveNextToGetString(int byteNum = 0)
        {
            if (byteNum < 0)
            {
                throw new ArgumentOutOfRangeException($"指定バイト数:{byteNum} バイト数は0以上にしてください");
            }
            var b = new List<byte>();
            if (byteNum == 0)
            {
                while (_payload.MoveNext())
                {
                    b.Add(_payload.Current);
                }
            }
            else
            {
                for (int i = 0; i < byteNum; i++)
                {
                    if (_payload.MoveNext())
                    {
                        b.Add(_payload.Current);
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("パケットフォーマットの解析に不具合があります");
                    }
                }
            }
            return Encoding.UTF8.GetString(b.ToArray());
        }
    }
}
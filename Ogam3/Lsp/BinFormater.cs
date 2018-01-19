﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Ogam3.Lsp {
    static class BinFormater {

        private struct Codes {
            public const byte Open = (byte)'(';
            public const byte Close = (byte)')';
            public const byte Integer16 = (byte)'i';
            public const byte Integer32 = (byte)'I';
            public const byte Integer64 = (byte)'l';
            public const byte Byte = (byte)'b';
            public const byte Bool = (byte)'B';
            public const byte Charter8 = (byte)'c';
            public const byte Charter32 = (byte)'C';
            public const byte Float32 = (byte)'f';
            public const byte Float64 = (byte)'F';
            public const byte SymbolShort = (byte)'s';
            public const byte SymbolLong = (byte)'S';
            public const byte String = (byte)'t';
            public const byte StreamShort = (byte)'r';
            public const byte StreamLong = (byte)'R';
            public const byte Null = (byte)'n';
            public const byte DateTime = (byte)'d';
        }

        public static Cons Read(MemoryStream data) {
            var stack = new Stack<dynamic>();
            var root = new Cons();
            stack.Push(root);
            var isQuote = false;

            while (true) {
                var b = data.ReadByte();

                if (b <= 0) {return root;} // EOS

                switch (b) {
                    case Codes.Open: {
                        var nod = new Cons();
                        stack.Peek().Add(nod);
                        stack.Push(nod);
                        break;
                    }
                    case Codes.Close:
                        stack.Pop();
                        break;
                        //FIXED SIZE
                    case Codes.Integer16:
                        stack.Peek().Add(BitConverter.ToInt16(R(data, 2), 0));
                        break;
                    case Codes.Integer32:
                        stack.Peek().Add(BitConverter.ToInt32(R(data, 4), 0));
                        break;
                    case Codes.Integer64:
                        stack.Peek().Add(BitConverter.ToInt64(R(data, 8), 0));
                        break;
                    case Codes.Byte:
                        stack.Peek().Add(data.ReadByte());
                        break;
                    case Codes.Bool:
                        stack.Peek().Add(data.ReadByte() != 0);
                        break;
                    case Codes.Charter8:
                        stack.Peek().Add((char)data.ReadByte());
                        break;
                    case Codes.Charter32:
                        stack.Peek().Add(Encoding.UTF32.GetChars(R(data, 4)).FirstOrDefault());
                        break;
                    case Codes.Float32:
                        stack.Peek().Add(BitConverter.ToSingle(R(data, 4), 0));
                        break;
                    case Codes.Float64:
                        stack.Peek().Add(BitConverter.ToDouble(R(data, 8), 0));
                        break;
                        //FLOAT SIZE
                    case Codes.SymbolShort:
                        stack.Peek().Add(new Symbol(Encoding.UTF8.GetString(R(data, data.ReadByte()))));
                        break;
                    case Codes.SymbolLong:
                        stack.Peek().Add(new Symbol(Encoding.UTF8.GetString(R(data, BitConverter.ToInt16(R(data, 2), 0)))));
                        break;
                    case Codes.String:
                        stack.Peek().Add(Encoding.UTF8.GetString(R(data, BitConverter.ToInt32(R(data, 4), 0))));
                        break;
                    case Codes.StreamShort:
                        stack.Peek().Add(new MemoryStream(R(data, BitConverter.ToInt32(R(data, 4), 0))));
                        break;
                    case Codes.Null:
                        stack.Peek().Add(null);
                        break;
                    case Codes.DateTime:
                        stack.Peek().Add(DateTime.FromBinary(BitConverter.ToInt64(R(data, 8), 0)));
                        break;
                    case Codes.StreamLong: // TODO
                        //var length = BitConverter.ToInt32(R(data, 4), 0);
                        //var ms = new MemoryStream();
                        //while (length > 0) {
                            
                        //}
                        throw new Exception("Not supported");
                        break;
                    case 'Q': {
                        var nod = new Cons(new Symbol("quote"));
                        stack.Peek().Add(nod);
                        stack.Push(nod);
                        break;
                    }
                    case 'V': {
                        var nod = new Cons(new Symbol("vector"));
                        stack.Peek().Add(nod);
                        stack.Push(nod);
                        break;
                    }
                        default:
                        throw new Exception("Bad data format!");
                            return null;
                }
            }


            return root;
        }

        private static byte[] R(Stream data, int count) {
            var buffer = new byte[count];
            var res = data.Read(buffer, 0, count);

            return res != count ? null : buffer;
        }

        public static MemoryStream Write(object tree) {
            var ms = new MemoryStream();

            WriteItem(ms, tree);

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        private static MemoryStream WriteConsSeq(MemoryStream ms, Cons tree) {
            ms.WriteByte(Codes.Open);

            foreach (var o in tree.GetIterator()) {
                    WriteItem(ms, o.Car());
            }

            ms.WriteByte(Codes.Close);

            return ms;
        }

        private static MemoryStream WriteItem(MemoryStream ms, object item) {
            var writeCode = new Action<byte>(ms.WriteByte);

            if (item is Cons) {
                WriteConsSeq(ms, item as Cons);
            } else if (item is int) {
                writeCode(Codes.Integer32);
                MsWrite(ms, BitConverter.GetBytes((int)item));
            } else if (item is uint) {
                writeCode(Codes.Integer32);
                MsWrite(ms, BitConverter.GetBytes((uint)item));
            } else if (item is long) {
                writeCode(Codes.Integer64);
                MsWrite(ms, BitConverter.GetBytes((long)item));
            } else if (item is ulong) {
                writeCode(Codes.Integer64);
                MsWrite(ms, BitConverter.GetBytes((ulong)item));
            } else if (item is short) {
                writeCode(Codes.Integer16);
                MsWrite(ms, BitConverter.GetBytes((short)item));
            } else if (item is ushort) {
                writeCode(Codes.Integer16);
                MsWrite(ms, BitConverter.GetBytes((ushort)item));
            } else if (item is float) {
                writeCode(Codes.Float32);
                MsWrite(ms, BitConverter.GetBytes((float)item));
            } else if (item is double) {
                writeCode(Codes.Float64);
                MsWrite(ms, BitConverter.GetBytes((double)item));
            } else if (item is byte) {
                writeCode(Codes.Byte);
                ms.WriteByte((byte)item);
            } else if (item is bool) {
                writeCode(Codes.Bool);
                ms.WriteByte((byte)((bool)item ? 1 : 0));
            } else if (item == null) {
                writeCode(Codes.Null);
            } else if (item is DateTime) {
                writeCode(Codes.DateTime);
                MsWrite(ms, BitConverter.GetBytes(((DateTime)item).ToBinary()));
            }else if (item is char) {
                if ((uint) item <= 255) {
                    writeCode(Codes.Charter8);
                    ms.WriteByte((byte)item);
                }
                else {
                    writeCode(Codes.Charter32);
                    MsWrite(ms, Encoding.UTF32.GetBytes(new []{(char) item}));
                }
            } else if (item is Symbol) {
                var bytes = Encoding.UTF8.GetBytes((item as Symbol).Name);

                if (bytes.Length <= 255) {
                    writeCode(Codes.SymbolShort);
                    ms.WriteByte((byte)bytes.Length);
                }
                else {
                    writeCode(Codes.SymbolLong);
                    MsWrite(ms, BitConverter.GetBytes((short)bytes.Length));
                }

                MsWrite(ms, bytes);
            } else if (item is string) {
                var bytes = Encoding.UTF8.GetBytes((item as string));
                writeCode(Codes.String);
                MsWrite(ms, BitConverter.GetBytes((int)bytes.Length));
                MsWrite(ms, bytes);
            } else if (item is Stream) {
                var bytes = ReadFully(item as Stream);
                writeCode(Codes.StreamShort);
                MsWrite(ms, bytes);
            }  else {
                throw new Exception($"The {item.GetType()} is unknown datatype");
            }

            return ms;
        }

        public static byte[] ReadFully(Stream input) {
            if (input is MemoryStream) {
                return (input as MemoryStream).ToArray();
            }

            using (var ms = new MemoryStream()) {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static MemoryStream MsWrite(MemoryStream ms, byte[] bytes) {
            ms.Write(bytes, 0, bytes.Length);
            return ms;
        }

        //private static MemoryStream MsWrite<T>(MemoryStream ms, T o) where T : struct {
        //    int size = Marshal.SizeOf(typeof(T));
        //    var bytes = new byte[size];
        //    var gcHandle = GCHandle.Alloc(o, GCHandleType.Pinned);
        //    Marshal.Copy(gcHandle.AddrOfPinnedObject(), bytes, 0, size);
        //    gcHandle.Free();
        //    return ms;
        //}

        private static byte[] Foo<T>(this T input) where T : struct {
            int size = Marshal.SizeOf(typeof(T));
            var result = new byte[size];
            var gcHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
            Marshal.Copy(gcHandle.AddrOfPinnedObject(), result, 0, size);
            gcHandle.Free();
            return result;
        }
    }
}
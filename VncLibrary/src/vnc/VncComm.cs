﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace VncLibrary
{
    public static class VncComm
    {
        #region Handshake
        public static VncEnum.Version ReadProtocolVersion(Stream a_stream, VncEnum.Version a_forceVersion)
        {
            byte[] buffer = new byte[12];
            a_stream.ReadAll(buffer, 0, buffer.Length);
            if (buffer.SequenceEqual(Encoding.ASCII.GetBytes("RFB 003.003\n"))
            ||  buffer.SequenceEqual(Encoding.ASCII.GetBytes("RFB 003.005\n")))
            {
                return VncEnum.Version.Version33;
            }
            else if (buffer.SequenceEqual(Encoding.ASCII.GetBytes("RFB 003.007\n")))
            {
                return VncEnum.Version.Version37;
            }
            else if (buffer.SequenceEqual(Encoding.ASCII.GetBytes("RFB 003.008\n")))
            {
                return VncEnum.Version.Version38;
            }

            // In rare cases, it may be a strange response, so if a_forceVersion is not Invalid, it is permitted if the beginning matches.
            if (a_forceVersion != VncEnum.Version.None)
            {
                if (Encoding.ASCII.GetString(buffer).Contains("RFB 003"))
                {
                    return a_forceVersion;
                }
            }

            throw new InvalidDataException($"Unknown version. Length = {buffer.Length} Text = [{Encoding.ASCII.GetString(buffer)}]");
        }

        public static void WriteProtocolVersion(Stream a_stream, VncEnum.Version a_version)
        {
            int version = 0;
            switch (a_version)
            {
            case VncEnum.Version.Version33:
                version = 3;
                break;
            case VncEnum.Version.Version37:
                version = 7;
                break;
            case VncEnum.Version.Version38:
                version = 8;
                break;
            default:
                throw new ArgumentException($"Unknown version. {a_version}");
            }

            byte[] buffer = Encoding.ASCII.GetBytes($"RFB 003.00{version}\n");
            a_stream.Write(buffer, 0, buffer.Length);
        }

        public static VncEnum.SecurityType ReadSecurityType(Stream a_stream)
        {
            byte[] buffer = new byte[4];
            a_stream.ReadAll(buffer, 0, buffer.Length);

            UInt32 securityType = BigEndianBitConverter.ToUInt32(buffer, 0);
            if (securityType == (UInt32)VncEnum.SecurityType.Invalid)
            {
                throw new SecurityException("Security type is Invalid.");
            }

            return (VncEnum.SecurityType)securityType;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="a_stream"></param>
        /// <returns></returns>
        /// <remarks>Use this function only for VNC Version 3.7 or later.</remarks>
        public static HashSet<VncEnum.SecurityType> ReadSecurityTypes(Stream a_stream)
        {
            // Read number-of-security-types
            byte[] buffer = new byte[1];
            a_stream.ReadAll(buffer, 0, buffer.Length);
            int numberOfSecurityTypes = buffer[0];

            if (numberOfSecurityTypes == 0)
            {
                string str = readReasonString(a_stream);
                throw new SecurityException($"Number-of-security-types is zero. {str}");
            }

            // Read security-types
            byte[] securityTypesBuffer = new byte[numberOfSecurityTypes];
            a_stream.ReadAll(securityTypesBuffer, 0, numberOfSecurityTypes);

            // Create return value
            var securityTypes = new HashSet<VncEnum.SecurityType>();
            for (int i = 0; i < numberOfSecurityTypes; ++i)
            {
                securityTypes.Add((VncEnum.SecurityType)securityTypesBuffer[i]);
            }

            return securityTypes;
        }

        public static void WriteSecurityType(Stream a_stream, VncEnum.SecurityType a_securityType)
        {
            byte[] buffer = new byte[1];
            buffer[0] = (byte)a_securityType;
            a_stream.Write(buffer, 0, buffer.Length);
        }

        public static VncEnum.SecurityResult ReadSecurityResult(Stream a_stream, VncEnum.Version a_vncVersion)
        {
            byte[] buffer = new byte[4];
            a_stream.ReadAll(buffer, 0, buffer.Length);

            UInt32 securityResult = BigEndianBitConverter.ToUInt32(buffer, 0);
            if (securityResult == (UInt32)VncEnum.SecurityResult.Failed)
            {
                if (a_vncVersion == VncEnum.Version.Version33
                ||  a_vncVersion == VncEnum.Version.Version37)
                {
                    throw new SecurityException("SecurityResult is Failed.");
                }
                else
                {
                    string str = readReasonString(a_stream);
                    throw new SecurityException($"SecurityResult is Failed. ({str})");
                }
            }

            return (VncEnum.SecurityResult)securityResult;
        }

        public static byte[] ReadVncChallange(Stream a_stream)
        {
            byte[] buffer = new byte[16];
            a_stream.ReadAll(buffer, 0, buffer.Length);

            return buffer;
        }

        public static void WriteVncResponse(Stream a_stream, byte[] a_response)
        {
            a_stream.Write(a_response, 0, a_response.Length);
        }
        #endregion

        #region Initial Message
        public static void WriteClientInit(Stream a_stream, VncEnum.SharedFlag a_sharedFlag)
        {
            byte[] buffer = new byte[1];
            buffer[0] = (byte)a_sharedFlag;
            a_stream.Write(buffer, 0, buffer.Length);
        }

        public static VncServerInitBody ReadServerInit(Stream a_stream)
        {
            // Read head for name-length
            byte[] buffer = new byte[24];
            a_stream.ReadAll(buffer, 0, buffer.Length);

            int len = (int)BigEndianBitConverter.ToUInt32(buffer, 20);

            // Read name-string
            byte[] str = new byte[len];
            a_stream.ReadAll(str, 0, len);

            // Create VncServerInitBody
            Array.Resize(ref buffer, 24 + len);
            Array.Copy(str, 0, buffer, 24, len);

            return new VncServerInitBody(buffer);
        }
        #endregion

        #region Client -> Server
        public static void WriteFramebufferUpdateRequest(Stream a_stream, VncEnum.FramebufferUpdateRequestIncremental a_incremental, UInt16 a_xpos, UInt16 a_ypos, UInt16 a_width, UInt16 a_height)
        {
            byte[] buffer = new byte[10];
            buffer[0] = (byte)VncEnum.MessageTypeClientToServer .FramebufferUpdateRequest; // 3
            buffer[1] = (byte)a_incremental;
            Array.Copy(BigEndianBitConverter.GetBytes(a_xpos),   0, buffer, 2, 2);
            Array.Copy(BigEndianBitConverter.GetBytes(a_ypos),   0, buffer, 4, 2);
            Array.Copy(BigEndianBitConverter.GetBytes(a_width),  0, buffer, 6, 2);
            Array.Copy(BigEndianBitConverter.GetBytes(a_height), 0, buffer, 8, 2);

            a_stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteSetPixelFormat(Stream a_stream, PixelFormat a_pixelFormat)
        {
            byte[] buffer = new byte[20];
            buffer[ 0] = (byte)VncEnum.MessageTypeClientToServer.SetPixelFormat; // 0
            buffer[ 1] = 0; // padding
            buffer[ 2] = 0; // padding
            buffer[ 3] = 0; // padding
            buffer[ 4] = (byte) (a_pixelFormat.BytesPerPixel * 8);
            buffer[ 5] = (byte) (a_pixelFormat.Depth);
            buffer[ 6] = (byte) (a_pixelFormat.BigEndianFlag ? 1 : 0);
            buffer[ 7] = (byte) (a_pixelFormat.TrueColorFlag ? 1 : 0);
            buffer[ 8] = (byte) (a_pixelFormat.RedMax >> 8);
            buffer[ 9] = (byte) (a_pixelFormat.RedMax & 0xFF);
            buffer[10] = (byte) (a_pixelFormat.GreenMax >> 8);
            buffer[11] = (byte) (a_pixelFormat.GreenMax & 0xFF);
            buffer[12] = (byte) (a_pixelFormat.BlueMax >> 8);
            buffer[13] = (byte) (a_pixelFormat.BlueMax & 0xFF);
            buffer[14] = (byte) (a_pixelFormat.RedShift);
            buffer[15] = (byte) (a_pixelFormat.GreenShift);
            buffer[16] = (byte) (a_pixelFormat.BlueShift);
            buffer[17] = 0; // padding
            buffer[18] = 0; // padding
            buffer[19] = 0; // padding

            a_stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteSetEncodings(Stream a_stream, VncEnum.EncodeType[] a_encodings)
        {
            byte[] buffer = new byte[4 + 4 * a_encodings.Length];
            buffer[0] = (byte)VncEnum.MessageTypeClientToServer.SetEncodings; // 2
            buffer[1] = 0; // padding
            Array.Copy(BigEndianBitConverter.GetBytes((UInt16)a_encodings.Length), 0, buffer, 2, 2);
            for (int i = 0; i < a_encodings.Length; ++i)
            {
                Array.Copy(BigEndianBitConverter.GetBytes((Int32)a_encodings[i]), 0, buffer, 4 + 4 * i, 4);
            }

            a_stream.Write(buffer, 0, buffer.Length);
        }

        public static void WritePointerEvent(Stream a_stream, VncEnum.PointerEventButtonMask a_buttonMask, UInt16 a_xpos, UInt16 a_ypos)
        {
            byte[] buffer = new byte[6];
            buffer[0] = (byte)VncEnum.MessageTypeClientToServer.PointerEvent; // 5
            buffer[1] = (byte)a_buttonMask;
            Array.Copy(BigEndianBitConverter.GetBytes(a_xpos), 0, buffer, 2, 2);
            Array.Copy(BigEndianBitConverter.GetBytes(a_ypos), 0, buffer, 4, 2);

            a_stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteKeyEvent(Stream a_stream, VncEnum.KeyEventDownFlag a_downFlag, UInt32 a_key)
        {
            byte[] buffer = new byte[8];
            buffer[0] = (byte)VncEnum.MessageTypeClientToServer.KeyEvent; // 4
            buffer[1] = (byte)a_downFlag;
            Array.Copy(BigEndianBitConverter.GetBytes(a_key), 0, buffer, 4, 4);

            a_stream.Write(buffer, 0, buffer.Length);
        }

        #endregion

        #region Server -> Client
        /// <summary>
        /// 
        /// </summary>
        /// <param name="a_stream"></param>
        /// <param name="a_bytesPerPixel">Use in Encode</param>
        /// <param name="a_isBigendian">Use in Encode</param>
        /// <returns></returns>
        public static byte[] ReadServerMessage(Stream a_stream, byte a_bytesPerPixel, bool a_isBigendian)
        {
            // Read message-type
            byte[] typeBuffer = new byte[1];
            a_stream.ReadAll(typeBuffer, 0, typeBuffer.Length);
            var messageType = (VncEnum.MessageTypeServerToClient)typeBuffer[0];

            // Read detail
            switch (messageType)
            {
            case VncEnum.MessageTypeServerToClient.FramebufferUpdate:
                var framebufferData = readFramebufferUpdate(a_stream, a_bytesPerPixel, a_isBigendian);
                framebufferData.Insert(0, typeBuffer);
                return concatByteArray(framebufferData);

            case VncEnum.MessageTypeServerToClient.SetColorMapEntries:
                var colorMap = readSetColorMapEntries(a_stream);
                colorMap.Insert(0, typeBuffer);
                return concatByteArray(colorMap);

            case VncEnum.MessageTypeServerToClient.Bell:
                return typeBuffer;

            case VncEnum.MessageTypeServerToClient.ServerCutText:
                var cutText = readServerCutText(a_stream);
                cutText.Insert(0, typeBuffer);
                return concatByteArray(cutText);

            case VncEnum.MessageTypeServerToClient.VMWare127:
            case VncEnum.MessageTypeServerToClient.Cloin_Dean_xvp:
            case VncEnum.MessageTypeServerToClient.Tight:
            case VncEnum.MessageTypeServerToClient.Gii:
            case VncEnum.MessageTypeServerToClient.VMWare254:
            case VncEnum.MessageTypeServerToClient.Authony_Liguori:
            default:
                throw new NotSupportedException($"Server-to-client message type is not supported. {messageType}");
            }
        }

        private static List<byte[]> readFramebufferUpdate(Stream a_stream, byte a_bytesPerPixel, bool a_isBigendian)
        {
            return VncEncodeFactory.CreateVncEncodeBinaryFromStream(a_stream, a_bytesPerPixel, a_isBigendian);
        }

        private static List<byte[]> readSetColorMapEntries(Stream a_stream)
        {
            var readDataList = new List<byte[]>();

            // Read padding(1) - first-color(2) - number-of-colors(2)
            byte[] head = new byte[5];
            a_stream.ReadAll(head, 0, head.Length);
            readDataList.Add(head);

            UInt16 numberOfColors = BigEndianBitConverter.ToUInt16(head, 3);

            // Read colors
            for (int i = 0; i < numberOfColors; ++i)
            {
                byte[] colors = new byte[6];
                a_stream.ReadAll(colors, 0, colors.Length);
                readDataList.Add(colors);
            }

            return readDataList;
        }

        private static List<byte[]> readServerCutText(Stream a_stream)
        {
            var readDataList = new List<byte[]>();

            // Read padding(3) - length(4)
            byte[] head = new byte[7];
            a_stream.ReadAll(head, 0, head.Length);
            readDataList.Add(head);

            UInt16 length = BigEndianBitConverter.ToUInt16(head, 3);

            // Read text(length)
            byte[] textBuffer = new byte[length];
            a_stream.ReadAll(textBuffer, 0, textBuffer.Length);
            readDataList.Add(textBuffer);

            return readDataList;
        }
        #endregion

        private static string readReasonString(Stream a_stream)
        {
            byte[] reasonLength = new byte[4];
            a_stream.ReadAll(reasonLength, 0, reasonLength.Length);
            UInt32 len = BigEndianBitConverter.ToUInt32(reasonLength, 0);

            byte[] reasonString = new byte[len];
            a_stream.ReadAll(reasonString, 0, (int)len);
            string str = Encoding.ASCII.GetString(reasonString);

            return str;
        }

        private static byte[] concatByteArray(IEnumerable<byte[]> a_byteArray)
        {
            int totalLength = a_byteArray.Sum(n => n.Length);

            int offset = 0;
            byte[] buffer = new byte[totalLength];
            foreach (var v in a_byteArray)
            {
                Buffer.BlockCopy(v, 0, buffer, offset, v.Length);
                offset += v.Length;
            }

            return buffer;
        }
    }
}

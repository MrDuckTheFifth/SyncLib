using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SyncLib {
    // Done by VMan.

    public static class RegistryChunkCodec {
        public sealed class Receiver {
            private string id;

            private string digest;

            private int length;

            private int received;

            private byte[][] chunks;

            private bool completed;

            public int ReceivedChunks => received;

            public int TotalChunks {
                get {
                    if (chunks != null) {
                        return chunks.Length;
                    }
                    return 0;
                }
            }

            public bool Accept(string envelope, out string json) {
                json = null;
                if (envelope == null || envelope.Length > 800 || !envelope.StartsWith("SLCH1|", StringComparison.Ordinal)) {
                    throw new InvalidDataException("Invalid chunk envelope");
                }
                // ATT's Mono runtime lacks Split(char, StringSplitOptions).
                string[] array = envelope.Split(new[] { '|' }, StringSplitOptions.None);
                if (array.Length != 7 || !Guid.TryParseExact(array[1], "N", out var _) || !int.TryParse(array[2], out var result2) || !int.TryParse(array[3], out var result3) || !int.TryParse(array[4], out var result4) || result4 <= 0 || result4 > 2097152 || result3 != (result4 + 384 - 1) / 384 || result2 < 0 || result2 >= result3 || array[5].Length != 64) {
                    throw new InvalidDataException("Invalid chunk metadata");
                }
                string text = array[5];
                for (int i = 0; i < text.Length; i++) {
                    if (!Uri.IsHexDigit(text[i])) {
                        throw new InvalidDataException("Invalid checksum");
                    }
                }
                byte[] array2 = Convert.FromBase64String(array[6]);
                int num = Math.Min(384, result4 - result2 * 384);
                if (array2.Length != num) {
                    throw new InvalidDataException("Invalid chunk length");
                }
                if (chunks == null) {
                    id = array[1];
                    digest = array[5];
                    length = result4;
                    chunks = new byte[result3][];
                }
                if (id != array[1] || digest != array[5] || length != result4 || chunks.Length != result3) {
                    throw new InvalidDataException("Inconsistent transfer metadata");
                }
                if (completed) {
                    return false;
                }
                if (chunks[result2] != null) {
                    if (Convert.ToBase64String(chunks[result2]) != array[6]) {
                        throw new InvalidDataException("Conflicting duplicate chunk");
                    }
                    return false;
                }
                chunks[result2] = array2;
                received++;
                if (received != result3) {
                    return false;
                }
                byte[] array3 = new byte[length];
                for (int j = 0; j < result3; j++) {
                    Buffer.BlockCopy(chunks[j], 0, array3, j * 384, chunks[j].Length);
                }
                if (!string.Equals(digest, Digest(array3), StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidDataException("Registry checksum mismatch");
                }
                json = Utf8.GetString(array3);
                completed = true;
                chunks = new byte[result3][];
                return true;
            }
        }

        public const string Prefix = "SLCH1|";

        public const int ChunkBytes = 384;

        public const int MaxBytes = 2097152;

        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        public static string[] Encode(string json, string transferId) {
            if (string.IsNullOrWhiteSpace(json)) {
                throw new ArgumentException("Empty registry");
            }
            if (!Guid.TryParseExact(transferId, "N", out var _)) {
                throw new ArgumentException("Invalid transfer ID");
            }
            byte[] bytes = Utf8.GetBytes(json);
            if (bytes.Length > 2097152) {
                throw new ArgumentException("Registry exceeds 2 MiB transfer limit");
            }
            string text = Digest(bytes);
            int num = (bytes.Length + 384 - 1) / 384;
            string[] array = new string[num];
            for (int i = 0; i < num; i++) {
                int num2 = i * 384;
                array[i] = "SLCH1|" + transferId + "|" + i + "|" + num + "|" + bytes.Length + "|" + text + "|" + Convert.ToBase64String(bytes, num2, Math.Min(384, bytes.Length - num2));
            }
            return array;
        }

        private static string Digest(byte[] bytes) {
            using SHA256 sHA = SHA256.Create();
            return BitConverter.ToString(sHA.ComputeHash(bytes)).Replace("-", "");
        }
    }
}
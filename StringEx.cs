using System;
using System.Text;

namespace PollBot {
    public static class StringEx {
        public static bool TryRemovePrefix(this string self, string prefix, out string rest) {
            if (self == null) {
                rest = null;
                return false;
            }
            if (self.ToLower().StartsWith(prefix.ToLower())) {
                rest = self.Remove(0, prefix.Length);
                return true;
            }
            Console.WriteLine($"Prefix is not found: {prefix} in {self}");
            rest = "";
            return false;
        }

        public static int UTF16Length(this string self) {
            return Encoding.Unicode.GetByteCount(self) / 2;
        }
    }
}
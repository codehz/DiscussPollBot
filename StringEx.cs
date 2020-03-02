using System.Text;

namespace PollBot {
    public static class StringEx {
        public static bool TryRemovePrefix(this string self, string prefix, out string rest) {
            if (self.StartsWith(prefix)) {
                rest = self.Remove(0, prefix.Length);
                return true;
            }
            rest = "";
            return false;
        }

        public static int UTF16Length(this string self) {
            return Encoding.Unicode.GetByteCount(self) / 2;
        }
    }
}
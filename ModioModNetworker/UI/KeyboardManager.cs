using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModioModNetworker.UI
{
    public class KeyboardManager
    {
        public static string typed = "";

        public static void Append(string character) {
            typed += character;
        }

        public static void Backspace() {
            typed = typed.Substring(0, typed.Length - 1);
        }
    }
}

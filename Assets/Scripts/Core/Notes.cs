using System;
using System.Collections.Generic;
using static PixelSpace.Game;

namespace PixelSpace
{
    /// <summary>Важное уведомление: нападения, потери, победы, вести от империй.</summary>
    [Serializable]
    public class Note
    {
        public string text;
        public int lv;     // уровень важности: Notes.ATTACK, LOSS, WIN, INFO
        public float t;    // игровое время
    }

    /// <summary>
    /// Журнал важных уведомлений. Пишем сюда только то, что важно помнить:
    /// нападения (жёлтые), сдачу своих планет и систем (красные),
    /// падение врагов, новые колонии, победы (зелёные) и вести от империй (синие).
    /// </summary>
    public static class Notes
    {
        public const int INFO = 0, ATTACK = 1, LOSS = 2, WIN = 3;
        public const int MAX = 300; // в журнале хранится столько последних уведомлений

        /// <summary>Новое уведомление — для экранного списка.</summary>
        public static Action<Note> Added;

        public static void Add(string text, int lv)
        {
            var n = new Note { text = text, lv = lv, t = G.time };
            G.notes.Add(n);
            if (G.notes.Count > MAX) G.notes.RemoveRange(0, G.notes.Count - MAX);
            Added?.Invoke(n);
        }

        public static string Col(int lv) => lv switch
        {
            ATTACK => "#ffcf4a",
            LOSS => "#ff5a6a",
            WIN => "#59ffb0",
            _ => "#6fb8ff",
        };

        /// <summary>Игровое время уведомления: «1:05:12» или «12:30».</summary>
        public static string Time(float t)
        {
            int s = (int)t, h = s / 3600, m = s / 60 % 60;
            s %= 60;
            return h > 0 ? h + ":" + m.ToString("00") + ":" + s.ToString("00") : m + ":" + s.ToString("00");
        }

        /// <summary>У империи не осталось систем — она пала.</summary>
        public static bool CheckFallen(int ei)
        {
            if (ei < 0 || ei >= G.emp.Count || G.emp[ei].fallen) return false;
            foreach (var s in G.stars) if (s.owner == ei) return false;
            G.emp[ei].fallen = true;
            return true;
        }
    }
}

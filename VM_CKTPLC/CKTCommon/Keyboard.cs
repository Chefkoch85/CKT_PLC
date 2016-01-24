using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Forms;

namespace CKT.INPUT
{
    public class Keyboard
    {
        static private Keyboard m_Instance = null;
        static public Keyboard instance
        {
            get
            {
                if(m_Instance != null)
                    return m_Instance;

                m_Instance = new Keyboard();
                m_Instance.init();

                return m_Instance;
            }
        }

        List<Keys> m_iaUsedKeys = null;
        bool[] m_baCurKeys = null;
        bool[] m_baOldKeys = null;

        static public readonly int MAX_KEYS = 255;

        public void init()
        {
            m_iaUsedKeys = new List<Keys>();
            m_baCurKeys = new bool[MAX_KEYS];
            m_baOldKeys = new bool[MAX_KEYS];
        }

        public void registerKey(Keys vKey)
        {
            if (!m_iaUsedKeys.Contains(vKey))
                m_iaUsedKeys.Add(vKey);
        }

        public void update()
        {
            for (int i = 0; i < MAX_KEYS; i++)
            {
                m_baOldKeys[i] = m_baCurKeys[i];
            }

            foreach (Keys k in m_iaUsedKeys)
            {
                if (AsyncKeyState.get(k) != 0)
                {
                    m_baCurKeys[(int)k] = true;
                }
                else
                {
                    m_baCurKeys[(int)k] = false;
                }
            }
        }

        public void flush()
        {
            foreach (Keys k in m_iaUsedKeys)
            {
                m_baCurKeys[(int)k] = false;
                m_baOldKeys[(int)k] = false;
            }
        }

        public bool isPress(Keys vKey)
        {
            return m_baCurKeys[(int)vKey];
        }
        public bool isDown(Keys vKey)
        {
            return m_baCurKeys[(int)vKey] && !m_baOldKeys[(int)vKey];
        }
        public bool isUp(Keys vKey)
        {
            return !m_baCurKeys[(int)vKey] && m_baOldKeys[(int)vKey];
        }
    }
}

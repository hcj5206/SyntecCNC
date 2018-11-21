using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using MySql.Data.MySqlClient;
using Syntec.Remote;
using System.Threading;

namespace SyntecCNC_Hanhai
{
    public partial class CNC_Control : Form
    {

        List<SyntecRemoteCNC> m_CNC;
        MySqlConnection mysql;
        SynchronizationContext m_SyncContext = null;

        public CNC_Control()
        {
            InitializeComponent();
            m_SyncContext = SynchronizationContext.Current;
            mysql = getMySqlCon();
            m_CNC = new List<SyntecRemoteCNC>();
            SyntecRemoteCNC cnc = new SyntecRemoteCNC(CNCIP.Default.一号机);
            //SyntecRemoteCNC cnc = new SyntecRemoteCNC("127.0.0.1");
            m_CNC.Add(cnc);
            cnc = new SyntecRemoteCNC(CNCIP.Default.二号机);
            m_CNC.Add(cnc);
            cnc = new SyntecRemoteCNC(CNCIP.Default.三号机);
            m_CNC.Add(cnc);
            cnc = new SyntecRemoteCNC(CNCIP.Default.四号机);
            m_CNC.Add(cnc);
            cnc = new SyntecRemoteCNC(CNCIP.Default.五号机);
            m_CNC.Add(cnc);
            cnc = new SyntecRemoteCNC(CNCIP.Default.六号机);
            m_CNC.Add(cnc);
            cnc = new SyntecRemoteCNC(CNCIP.Default.七号机);
            m_CNC.Add(cnc);
            cnc = new SyntecRemoteCNC(CNCIP.Default.八号机);
            m_CNC.Add(cnc);
            int i = 0;
            foreach (SyntecRemoteCNC tmp in m_CNC)
            {
                i++;
                listBox1.Items.Add(i+"号:"+tmp.Host);
            }
            /*OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
                MessageBox.Show(ofd.FileName);*/
            Thread sql_thread = new Thread(new ThreadStart(DBListen));
            sql_thread.Start();
        }

        public MySqlConnection getMySqlCon()
        {
            String mysqlStr = "Database='" + DBConfig.Default.NAME + "';Data Source='" + DBConfig.Default.IP + "';User Id='" + DBConfig.Default.USER + "';Password='" + DBConfig.Default.PASS + "';CharSet='utf8'";
            MySqlConnection mysql = new MySqlConnection(mysqlStr);
            return mysql;
        }

        public void DBListen()
        {
            while (true)
            {
                int Index = 0;
                Boolean Success_flag = false;
                String SQLSearch = "SELECT `Machine_num`,`NC_PATH`,`Index` FROM `work_cnc_task_list` WHERE `CNC`=5";
                MySqlCommand mySqlCommand = new MySqlCommand(SQLSearch, mysql);
                try
                {
                    mysql.Open();
                    MySqlDataReader reader = mySqlCommand.ExecuteReader();
                    if (reader.Read())
                    {
                        if (reader.HasRows)
                        {
                            int Machine_num = reader.GetInt32(0);
                            String Filepath = reader.GetString(1);
                         
                            Index = reader.GetInt32(2);
                            m_SyncContext.Post(Log, "准备上传文件" + Filepath + "至" + Machine_num +"号机床！");
                            SyntecRemoteCNC cnc = m_CNC[Machine_num - 1];
                            short result = cnc.UPLOAD_nc_mem(Filepath);
                            
                            if (result == (short)SyntecRemoteCNC.ErrorCode.NormalTermination)
                            {
                                WaitForUploadFile(cnc);
                            }
                            else
                            {
                                result = cnc.UPLOAD_nc_mem(Filepath);
                                if (result == (short)SyntecRemoteCNC.ErrorCode.NormalTermination)
                                {
                                    WaitForUploadFile(cnc);
                                }
                                else
                                    m_SyncContext.Post(Log, "NC文件上传失败！" + result);
                            }

                            string filename = Filepath.Substring(Filepath.LastIndexOf("\\") + 1, Filepath.LastIndexOf(".") - (Filepath.LastIndexOf("\\") + 1)) +"."+ Filepath.Substring(Filepath.LastIndexOf(".") + 1, Filepath.Length - Filepath.LastIndexOf(".") - 1);
                            result = cnc.WRITE_nc_main(filename);//上传指定
                            int fail_num = 0;
                            int CurSeq = 0;
                            string MainProg = "", CurProg = "", Mode = "", Status = "", Alarm = "", EMG = "";
                            short result1 = cnc.READ_status(out MainProg, out CurProg, out CurSeq, out Mode, out Status, out Alarm, out EMG);
        
                            if (result == (short)SyntecRemoteCNC.ErrorCode.NormalTermination&&MainProg==filename)
                            {
                                m_SyncContext.Post(Log, cnc.Host + ":加工文件指定成功:" + filename);
                                Success_flag = true;
                            }
                            else
                            {
                                while (MainProg != filename)
                                {
                                    Console.WriteLine("ERROR");
                                    cnc.WRITE_nc_main(filename);
                                    cnc.READ_status(out MainProg, out CurProg, out CurSeq, out Mode, out Status, out Alarm, out EMG);
                                    Thread.Sleep(10);
                                    fail_num++;
                                    if (fail_num==100)
                                    {
                                        m_SyncContext.Post(Log, cnc.Host + ":加工文件指定失败:" + result.ToString() + "-" + filename);
                                        break;
                                    }
                                }
                                m_SyncContext.Post(Log, cnc.Host + ":加工文件指定成功:" + filename);
                                Success_flag = true;
                            }
                        }
                    }
                }
                catch (MySqlException ex)
                {
                    m_SyncContext.Post(Log, "数据库读取失败！原因：" + ex.ToString());
                }
                finally
                {
                    mysql.Close();
                }
                if(Success_flag)
                    Sql_do("UPDATE `work_cnc_task_list` SET `CNC`=10 WHERE `Index`=" + Index);
                else
                    Sql_do("UPDATE `work_cnc_task_list` SET `CNC`=1000 WHERE `Index`=" + Index);
                Thread.Sleep(10);
            }
        }

        private void WaitForUploadFile(SyntecRemoteCNC cnc)
        {
            while (!cnc.isFileUploadDone)
            {

                Thread.Sleep(50);
                Console.WriteLine("try_load");
            }
            m_SyncContext.Post(Log, cnc.Host + " 加工文件上传成功！");
        }

        private void toolStripButton1_Click(object sender, EventArgs e)//工具栏添加机台
        {
            string cncip = Interaction.InputBox("请输入机台IP", "添加机台", "192.168.31.XXX", 100, 100);

            SyntecRemoteCNC cnc = new SyntecRemoteCNC(cncip);
            m_CNC.Add(cnc);
            listBox1.Items.Add(cnc.Host);
        }

        private void toolStripButton2_Click(object sender, EventArgs e)//工具栏数据库设置
        {
            DBConfig.Default.IP = Interaction.InputBox("请输入服务器IP", "数据库服务器修改", DBConfig.Default.IP, 100, 100);
            DBConfig.Default.NAME = Interaction.InputBox("请输入数据库名", "数据库服务器修改", DBConfig.Default.NAME, 100, 100);
            DBConfig.Default.USER = Interaction.InputBox("请输入登陆用户名", "数据库服务器修改", DBConfig.Default.USER, 100, 100);
            DBConfig.Default.PASS = Interaction.InputBox("请输入登陆密码", "数据库服务器修改", DBConfig.Default.PASS, 100, 100);
            DBConfig.Default.Save();
            mysql = getMySqlCon();
        }

        public void Log(Object log_info)
        {
            String tolog = (String)log_info;
            LogBox1.AppendText(tolog + "\r\n");
            LogBox1.ScrollToCaret();
        }

        private bool Sql_do(String command)
        {
            MySqlCommand mySqlCommand = new MySqlCommand(command, mysql);
            try
            {
                mysql.Open();
                mySqlCommand.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("MySqlException Error3:" + ex.ToString());
                return true;
            }
            finally
            {
                mysql.Close();
            }
            return true;
        }

        private void CNC_Control_FormClosing(object sender, FormClosingEventArgs e)
        {
            System.Environment.Exit(0);
        }

        private void test_but_Click(object sender, EventArgs e)
        {
            foreach( SyntecRemoteCNC cnc in m_CNC ) {
				int CurSeq = 0;
				string MainProg = "", CurProg = "", Mode = "", Status = "", Alarm = "", EMG = "";

				short result = cnc.READ_status( out MainProg, out CurProg, out CurSeq, out Mode, out Status, out Alarm, out EMG );
				if( result == (short)SyntecRemoteCNC.ErrorCode.NormalTermination ) {
					Log( "===" + cnc.Host + "===" );
					Log( "MainProg : " + MainProg.ToString() );
					Log( "CurrentProg : " + CurProg.ToString() );
					Log( "CurrentSeq : " + CurSeq.ToString() );
					Log( "Mode : " + Mode.ToString() );
					Log( "Status : " + Status.ToString() );
					Log( "Alarm : " + Alarm.ToString() );
					Log( "EMG : " + EMG.ToString() );
				}
				else {
					Log( cnc.Host + " : Error:testREAD_status:" + result.ToString() );
				}
			}
            
        }


    }
}

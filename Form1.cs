using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using ZedGraph;

using NationalInstruments.DAQmx;
using System.Diagnostics;

namespace Sungwhans_Tester_ver._1
{
public partial class Form1 : Form
    {
        Stopwatch _sw;

        [DllImport(@"C:\Users\matar\Desktop\Sungwhans_Tester_ver.1\Sungwhans_Tester_ver.1\resource\EIB7_Library_CS.dll")]
        unsafe public static extern int EIB_OPEN(int* eib1);
        [DllImport(@"C:\Users\matar\Desktop\Sungwhans_Tester_ver.1\Sungwhans_Tester_ver.1\resource\EIB7_Library_CS.dll")]
        unsafe public static extern void EIB_GET_DATA(int axis_1, double* angle_variable);


        double M_torque_Cal = 0.47904, M_Speed_Cal = 0.005 , input_torque_cal, output_torque_cal;
        Thread test_1, test_2, zed_graph_1, EIB_out_RPM;
        double[,] voltage_data;
        double[] AO_DATA = {0, 0, 0, 0};
        double safe_eib_var=0, zero_safe_eib_var=0, zero_input_1_var=0 ,zero_input_2_var=0;

        double Input_torque=0,output_torque = 0,Efficiency=0; // PID 때문에 얘는 밖으로 내보내서 전역변수화 시켜야 함

        int manual_drive = 0, ctr_1=0, CCW_state=1, Specimen_state=1;

        /**************************PID_val******************************/
        double out_val=0, old_out_val=0;
        double[] error_val=new double[2];
        /******************************************************************/
        /* Ni DAQ Task 설정*/
        NationalInstruments.DAQmx.Task NI_MYTASK=new NationalInstruments.DAQmx.Task();
        AnalogMultiChannelReader analogreader;
        NationalInstruments.DAQmx.Task running_task = new NationalInstruments.DAQmx.Task();
        AnalogMultiChannelWriter analogshooter;
        
        AsyncCallback analogcallback;

        // zed graph 
        LineItem _lineitem1, _lineItem2, _lineItem3;
        PointPairList _pointpairlist_1, _pointpairlist_2, _pointpairlist_3;
        GraphPane _graphpane_1,_graphpane_2, _graphpane_3;
        // icon
        string icon_start_direction = @"C:\Users\matar\Desktop\Sungwhans_Tester_ver.1.6\Sungwhans_Tester_ver.1\icon_res\EIB_Run.ico",
               icon_Stop_direction = @"C:\Users\matar\Desktop\Sungwhans_Tester_ver.1.6\Sungwhans_Tester_ver.1\icon_res\EIB_Stop.ico";

        // graph
        double pulse_per_loop = 0, Initial_pulse= 0;

        public Form1()
        {
            InitializeComponent();
            Run_BTN.Image = Image.FromFile(icon_start_direction);
            groupBox1.Enabled = false;

            toolStripStatusLabel1.Text = "Initialized";
            toolStripStatusLabel1.BackColor = Color.Gray;
            toolStripStatusLabel1.ForeColor=  Color.White;
            toolStripStatusLabel2.Text = "Save: Not allocated";
            toolStripStatusLabel2.BackColor = Color.Red;
            toolStripStatusLabel2.ForeColor = Color.White;

            /*Combobox에 할당*/
            comboBox1.Items.AddRange(NationalInstruments.DAQmx.DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AI, PhysicalChannelAccess.External));
            comboBox2.Items.AddRange(NationalInstruments.DAQmx.DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AO, PhysicalChannelAccess.External));
            if (comboBox1.Items.Count > 0)comboBox1.SelectedIndex = 0;
            if (comboBox2.Items.Count > 0)comboBox2.SelectedIndex = 0;

            /// zed graph out_torque 
            _graphpane_1 = zedGraphControl1.GraphPane;
            _graphpane_1.Title.Text = "Input_Torque";
            _graphpane_1.Fill = new Fill(Color.White, Color.White, 180.0f);
            _graphpane_1.XAxis.Title.Text = "Time(sec)";
            _graphpane_1.YAxis.Title.Text = "InPut_Torque(Nm)";
            _graphpane_1.XAxis.MajorGrid.IsVisible = true;
            _graphpane_1.YAxis.MajorGrid.IsVisible = true;
            _graphpane_1.XAxis.MinorGrid.IsVisible = true;
            _graphpane_1.YAxis.MinorGrid.IsVisible = true;

            _graphpane_2 = zedGraphControl2.GraphPane;
            _graphpane_2.Title.Text = "Output_Torque";
            _graphpane_2.Fill = new Fill(Color.White, Color.White, 180.0f);
            _graphpane_2.XAxis.Title.Text = "Time(sec)";
            _graphpane_2.YAxis.Title.Text = "Output_Torque(Nm)";
            _graphpane_2.XAxis.MajorGrid.IsVisible = true;
            _graphpane_2.YAxis.MajorGrid.IsVisible = true;
            _graphpane_2.XAxis.MinorGrid.IsVisible = true;
            _graphpane_2.YAxis.MinorGrid.IsVisible = true;

            _graphpane_3 = zedGraphControl3.GraphPane;
            _graphpane_3.Title.Text = "Efficiency";
            _graphpane_3.Fill = new Fill(Color.White, Color.White, 180.0f);
            _graphpane_3.XAxis.Title.Text = "Time(sec)";
            _graphpane_3.YAxis.Title.Text = "Efficiency(%)";
            _graphpane_3.XAxis.MajorGrid.IsVisible = true;
            _graphpane_3.YAxis.MajorGrid.IsVisible = true;
            _graphpane_3.XAxis.MinorGrid.IsVisible = true;
            _graphpane_3.YAxis.MinorGrid.IsVisible = true;
            ///
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (test_1 != null) test_1.Abort();
            if (test_2 != null) test_2.Abort();
            if (zed_graph_1 != null) zed_graph_1.Abort();
            if (EIB_out_RPM != null) EIB_out_RPM.Abort();

            Application.Exit();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (test_1 != null) test_1.Abort();
            if (test_2 != null) test_2.Abort();
            if (zed_graph_1 != null) zed_graph_1.Abort();
            if (EIB_out_RPM != null) EIB_out_RPM.Abort();

            Thread indicator = new Thread(indicator_1);
            test_2 = indicator;

            EIB_out_RPM=new Thread(OUT_RPM_FUNCTION);

            toolStripStatusLabel1.Text = "Connected";

            toolStripStatusLabel1.BackColor = Color.Green;
            toolStripStatusLabel1.ForeColor = Color.White;
            int eib_test;

            try
            {
                /***********************nidaq 접속*******************************/
                //NI_MYTASK.AIChannels.CreateVoltageChannel(comboBox1.Text,"", AITerminalConfiguration.Differential, -10, 10, AIVoltageUnits.Volts);
                NI_MYTASK.AIChannels.CreateVoltageChannel("cDAQ2Mod4/ai0:3", "", AITerminalConfiguration.Differential, -10, 10, AIVoltageUnits.Volts);
                NI_MYTASK.Timing.ConfigureSampleClock("", 100, SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples,100);
                
                analogreader = new AnalogMultiChannelReader(NI_MYTASK.Stream);
                analogreader.SynchronizeCallbacks = true;

                analogcallback = new AsyncCallback(analogincallback);
                analogreader.BeginReadMultiSample(100, analogcallback, NI_MYTASK);

                //running_task.AOChannels.CreateVoltageChannel(comboBox2.Text,"", -10, 10, AOVoltageUnits.Volts);
                running_task.AOChannels.CreateVoltageChannel("cDAQ2Mod2/ao0:3", "", -10, 10, AOVoltageUnits.Volts);
                analogshooter = new AnalogMultiChannelWriter(running_task.Stream);

                indicator.Start();
                EIB_out_RPM.Start();
                /**************************************************************/
                for (int i = 0; i < 4; i++)
                AO_DATA[i] = 0;
                analogshooter.WriteSingleSample(true, AO_DATA);
                /***********************EIB 접속*******************************/
                unsafe { EIB_OPEN(&eib_test); }
                /*********************그룹박스활성*****************************/
                groupBox1.Enabled = true;
                /**************************************************************/
            }
            catch
            {
                MessageBox.Show("접속에 문제발생");
            }
        }

        /******************************************************************************************************************************************************************************************************/
        void analogincallback(IAsyncResult ar)
        {
            double eib_var; 
            if ((NI_MYTASK != null)&&(NI_MYTASK==ar.AsyncState))
            voltage_data = analogreader.EndReadMultiSample(ar);

            unsafe
            {
                EIB_GET_DATA(1, &eib_var);
                safe_eib_var = eib_var;
            }
            analogreader.BeginReadMultiSample(100, analogcallback, NI_MYTASK);
            Invoke(new MethodInvoker(delegate () {

                // voltage_calibration_data input
                input_torque_cal = Convert.ToDouble(label12.Text);
                output_torque_cal = Convert.ToDouble(label13.Text);

                label22.Text = (CCW_state*(voltage_data[0, 0]-zero_input_1_var)).ToString("f1");

                label23.Text = (-1*Specimen_state * CCW_state * (voltage_data[1, 0] - zero_input_2_var)).ToString("f1"); ;
                

                label24.Text = voltage_data[2, 0].ToString("f1");

                Input_torque = (2.35 * CCW_state * (voltage_data[0, 0] - zero_input_1_var));
                label6.Text = Input_torque.ToString("f1");
                output_torque = (53.33*-1 * Specimen_state * CCW_state * (voltage_data[1, 0] - zero_input_2_var));
                label7.Text = output_torque.ToString("f1");
                label14.Text = (-1*CCW_state*Specimen_state*((safe_eib_var-zero_safe_eib_var)/67108864*360)).ToString("f1"); // 각도 Bit 적용 후 

                if (ctr_1 == 1)
                Efficiency=((53.33 * -1 * Specimen_state * CCW_state * (voltage_data[1, 0] - zero_input_2_var)) / (2.35 * CCW_state * (voltage_data[0, 0] - zero_input_1_var)*Convert.ToDouble(textBox14.Text))*100);
                label15.Text = Efficiency.ToString("f1");
            }));
        }
        public void OUT_RPM_FUNCTION()
        {
                do
                {
                    pulse_per_loop = ((-1 * CCW_state * Specimen_state * (safe_eib_var - zero_safe_eib_var)));
                    label13.Text = ((((-1 * CCW_state * Specimen_state * (safe_eib_var - zero_safe_eib_var)) - Initial_pulse )* 60) / 67108864).ToString("f3");

                    Thread.Sleep(1000);

                    Initial_pulse = pulse_per_loop;
                } while (true);   
        }

        private void BTN_L_MouseDown(object sender, MouseEventArgs e)
        {
            
            if (e.Button == MouseButtons.Left)
            {
                if (manual_drive == 1)
                {
                    AO_DATA[0] = Convert.ToDouble(textBox3.Text) * M_torque_Cal;
                    AO_DATA[1] = Convert.ToDouble(textBox4.Text) * M_Speed_Cal;
                    AO_DATA[2] = 0;
                    AO_DATA[3] = 0;
                    analogshooter.WriteSingleSample(true, AO_DATA);
                }
            }            
        }
        private void BTN_L_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            { 
                if(manual_drive==1)
                {
                    AO_DATA[0] = 1;
                    for (int i = 1; i < 4; i++)
                        AO_DATA[i] = 0;
                    analogshooter.WriteSingleSample(true, AO_DATA);
                    for (int i = 0; i < 4; i++)
                        AO_DATA[i] = 0;
                    analogshooter.WriteSingleSample(true, AO_DATA);
                }
            }
        }

        private void BTN_R_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (manual_drive == 1)
                {
                    AO_DATA[0] = Convert.ToDouble(textBox3.Text) * M_torque_Cal * -1;
                    AO_DATA[1] = Convert.ToDouble(textBox4.Text) * M_Speed_Cal;
                    AO_DATA[2] = 0;
                    AO_DATA[3] = 0;
                    analogshooter.WriteSingleSample(true, AO_DATA);
                }
            }            
        }
        private void BTN_R_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if(manual_drive==1)
                {

                    AO_DATA[0] = 1;
                    for (int i = 1; i < 4; i++)
                        AO_DATA[i] = 0;
                    analogshooter.WriteSingleSample(true, AO_DATA);
                    for (int i = 0; i < 4; i++)
                        AO_DATA[i] = 0;
                    analogshooter.WriteSingleSample(true, AO_DATA);

                }
            }
        }

        private void BTN_Discon_Click(object sender, EventArgs e)
        {
            AO_DATA[0] = 1;
            for (int i = 1; i < 4; i++)
                AO_DATA[i] = 0;
            analogshooter.WriteSingleSample(true, AO_DATA);
            for (int i = 0; i < 4; i++)
                AO_DATA[i] = 0;
            analogshooter.WriteSingleSample(true, AO_DATA);

            if (test_1 != null) test_1.Abort();
            if (test_2 != null) test_2.Abort();
            if (zed_graph_1 != null) zed_graph_1.Abort();
            if (EIB_out_RPM != null) EIB_out_RPM.Abort();

            toolStripStatusLabel1.Text = "Disconnected";
            toolStripStatusLabel1.BackColor = Color.Red;
            toolStripStatusLabel1.ForeColor = Color.White;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            zero_safe_eib_var = safe_eib_var;
            zero_input_1_var = voltage_data[0, 0];
            zero_input_2_var = voltage_data[1, 0];
        }
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (manual_drive == 0)
            {
                manual_drive = 1;
                groupBox1.Enabled = false;
            }
            else
            {
                manual_drive = 0;
                groupBox1.Enabled = true;
            }
        }


        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {

            if (CCW_state == 1)
                CCW_state = -1;
            else
                CCW_state = 1;
        }
        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (Specimen_state == 1)
                Specimen_state = -1;
            else
                Specimen_state = 1;
        }

        private void Run_BTN_Click_2(object sender, EventArgs e)
        {
            if (ctr_1==0)
            {
                if(_pointpairlist_1!=null)
                {
                    _pointpairlist_1.Clear();
                    _lineitem1.Clear();
                    _graphpane_1.CurveList.Clear();
                }
                if (_pointpairlist_2 != null)
                {
                    _pointpairlist_2.Clear();
                    _lineItem2.Clear();
                    _graphpane_2.CurveList.Clear();
                }
                if (_pointpairlist_3 != null)
                {
                    _pointpairlist_3.Clear();
                    _lineItem3.Clear();
                    _graphpane_3.CurveList.Clear();
                }
                ctr_1 = 1;
                Run_BTN.Image = Image.FromFile(icon_Stop_direction);
                zed_graph_1 = new Thread(graph_of_zed_1);
                test_1 = new Thread(save_data_function);
                zed_graph_1.Start();
                test_1.Start();
            }
            else if (ctr_1 == 1)
            {
                ctr_1 = 0;
                Run_BTN.Image = Image.FromFile(icon_start_direction);
                zed_graph_1.Abort();
                test_1.Abort();
                toolStripStatusLabel2.Text = "Save: Not allocated";
                toolStripStatusLabel2.BackColor = Color.Red;
                toolStripStatusLabel2.ForeColor = Color.White;
            }
        }
        // SAVE Data
        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog save_file_1 = new SaveFileDialog())
            {
                save_file_1.Filter = "Dat_file.(*.dat)|*.dat";
                save_file_1.FilterIndex = 1;
                if (save_file_1.ShowDialog() == DialogResult.OK)
                {
                    toolStripStatusLabel2.Text = save_file_1.FileName;
                    toolStripStatusLabel2.BackColor = Color.Green;
                    toolStripStatusLabel2.ForeColor = Color.White;
                }
            }
        }
        public void save_data_function() //Back Working Thread_1
        {
            if (toolStripStatusLabel2.Text != "Save: Not allocated")
            {
                using (StreamWriter out_file=new StreamWriter(toolStripStatusLabel2.Text))
                {
                    out_file.WriteLine("Time\tInput_Torque(Nm)\tOutput_Torque(Nm)\tEfficiency(%)");
                    do
                    {
                        out_file.WriteLine((Convert.ToDouble(_sw.ElapsedMilliseconds) / 1000).ToString("f3") + "\t" + Input_torque.ToString("f1") + "\t" + output_torque.ToString("f1") + "\t" + label15.Text);
                        Thread.Sleep(10);
                    } while (true);
                }
            }
        }
        /******************************************************************************************************************************************************************************************************/
        public void indicator_1() //Back Working Thread_2
        {
            do
            {
                if(manual_drive==0)
                {
                    if (ctr_1 == 0)
                    {
                        for (int i = 0; i < 4; i++) AO_DATA[i] = 0;
                        analogshooter.WriteSingleSample(true, AO_DATA);
                    }
                    else if (ctr_1 == 1)
                    {
                        AO_DATA[0] = CCW_state * Convert.ToDouble(textBox1.Text) * M_torque_Cal;
                        AO_DATA[1] = Convert.ToDouble(textBox2.Text) * M_Speed_Cal;
                        if (textBox6.Text == "")
                            AO_DATA[2] = 0;
                        else
                            AO_DATA[2] = control_as_pid(Convert.ToDouble(textBox7.Text), Convert.ToDouble(textBox9.Text), Convert.ToDouble(textBox8.Text), Convert.ToDouble(textBox6.Text), output_torque);

                        AO_DATA[3] = 0;
                        analogshooter.WriteSingleSample(true, AO_DATA);
                        if ((textBox5.Text != "")&&(Math.Abs(Convert.ToDouble(label14.Text)) > Convert.ToDouble(textBox5.Text)))
                        {
                            Run_BTN_Click_2(this, null);
                        }
                    }
                }
                Invoke(new MethodInvoker(delegate ()
                {
                    label31.Text = ctr_1.ToString();
                    label40.Text = CCW_state.ToString();
                    label41.Text = Specimen_state.ToString();
                }));                
            } while (true);
        }

        public void graph_of_zed_1()
        {
            _sw = new Stopwatch();
            
            _pointpairlist_1 = new PointPairList();
            _lineitem1 = _graphpane_1.AddCurve("Input_Torque", _pointpairlist_1, Color.Red, SymbolType.None);

            _pointpairlist_2 = new PointPairList();
            _lineItem2 = _graphpane_2.AddCurve("Output_Torque", _pointpairlist_2, Color.Red, SymbolType.None);

            _pointpairlist_3 = new PointPairList();
            _lineItem3 = _graphpane_3.AddCurve("Efficiency", _pointpairlist_3, Color.Red, SymbolType.None);

            _sw.Start();
            do
            {
                double x = Convert.ToDouble(_sw.ElapsedMilliseconds);
                double y1 = Input_torque, y2 = output_torque, y3 = Efficiency;

                _pointpairlist_1.Add(x, y1);
                _pointpairlist_2.Add(x, y2);
                _pointpairlist_3.Add(x, y3);

                zedGraphControl1.Refresh();
                zedGraphControl2.Refresh();
                zedGraphControl3.Refresh();

                zedGraphControl1.AxisChange();
                zedGraphControl2.AxisChange();
                zedGraphControl3.AxisChange();
            } while (true);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (test_1 != null) test_1.Abort();
            if (test_2 != null) test_2.Abort();
            if (zed_graph_1 != null) zed_graph_1.Abort();
            if (EIB_out_RPM != null) EIB_out_RPM.Abort();
            Application.Exit();
        }

        private double control_as_pid(double P_val, double D_val, double I_val, double set_point, double Out_torque)
        {
            double proportional_data_fn = 0, integral_data_fn = 0, deriviative_data_fn = 0;

            error_val[0] = set_point - Out_torque;

            proportional_data_fn = P_val * error_val[0];
            integral_data_fn = I_val * (error_val[0] + error_val[1]);
            deriviative_data_fn = D_val * (error_val[0] - error_val[1]);

            out_val = proportional_data_fn + integral_data_fn + deriviative_data_fn + old_out_val;
            error_val[1] = error_val[0];

            out_val = (out_val > 10) ? 10 : out_val;
            out_val = (out_val < -10) ? -10 : out_val;

            old_out_val= out_val;

            return out_val;
        }
    }
}

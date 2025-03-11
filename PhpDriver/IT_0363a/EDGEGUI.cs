using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Utilities;

namespace Driver.IT_0363a
{
    public partial class EDGEGUI : Form
    {
        IT_0363ACAEdgeReporting origin;

        public EDGEGUI(IT_0363ACAEdgeReporting caller)
        {
            origin = caller;
            InitializeComponent();

        }

        private void Exitbtn_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void EDGEGUI_Load(object sender, EventArgs e)
        {
            ReportYearTxtBx.Text = DateTime.Today.ToString("yyyy");

        }

        private void SelectAllChk_CheckedChanged(object sender, EventArgs e)
        {
            EnrollmentAllChk.Checked = SelectAllChk.Checked;
            MedAllChk.Checked = SelectAllChk.Checked;
            PharmAllChk.Checked = SelectAllChk.Checked;
            SubmissionAllChk.Checked = SelectAllChk.Checked;
            SendToEdgeAllChk.Checked = SelectAllChk.Checked;
            GetAllEDGEChk.Checked = SelectAllChk.Checked;
            CreateAllReportsChk.Checked = SelectAllChk.Checked;
            CloseAfterChk.Checked = SelectAllChk.Checked;

            if (SelectAllChk.Checked)
            {
                DialogResult result1;
                MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                result1 = MessageBox.Show("You are choosing to run ALL EDGE processing. Are you SURE you want to include Enrollment?", "ALERT", buttons);
                if (result1 == DialogResult.Yes)
                {
                    EnrollmentAllChk.Checked = true;
                }
                else
                {
                    SelectAllChk.Checked = false;
                    MedAllChk.Checked = true;
                    PharmAllChk.Checked = true;
                    CloseAfterChk.Checked = true;
                }
            }

            //disable other buttons because check all was selected
            if (SelectAllChk.Checked)
            {
                EnrollmentAllChk.Enabled = false;
                MedAllChk.Enabled = false;
                PharmAllChk.Enabled = false;
                SubmissionAllChk.Enabled = false;
                SendToEdgeAllChk.Enabled = false;
                GetAllEDGEChk.Enabled = false;
                CreateAllReportsChk.Enabled = false;
                SubmissionSupplementalChk.Enabled = false;
                SupplementalAllChk.Enabled = false;
                SendToEdgeSupplementalChk.Enabled = false;
                GetSupplementalEDGEChk.Enabled = false;
                CreateSupplementalReportsChk.Enabled = false;

            }
            else
            {
                EnrollmentAllChk.Enabled = true;
                EnrollmentAllChk.Enabled = true;
                MedAllChk.Enabled = true;
                PharmAllChk.Enabled = true;
                SubmissionAllChk.Enabled = true;
                SendToEdgeAllChk.Enabled = true;
                GetAllEDGEChk.Enabled = true;
                CreateAllReportsChk.Enabled = true;
                SubmissionSupplementalChk.Enabled = true;
                SupplementalAllChk.Enabled = true;
                SendToEdgeSupplementalChk.Enabled = true;
                GetSupplementalEDGEChk.Enabled = true;
                CreateSupplementalReportsChk.Enabled = true;
            }
        }

        private void EnrollmentAllChk_CheckedChanged(object sender, EventArgs e)
        {
            SubmissionEnrollmentChk.Checked = EnrollmentAllChk.Checked;
            SendToEdgeEnrollmentChk.Checked = EnrollmentAllChk.Checked;
            GetEnrollmentEDGEChk.Checked = EnrollmentAllChk.Checked;
            CreateEnrollmentReportsChk.Checked = EnrollmentAllChk.Checked;

            //disable other buttons because check all was selected
            if (EnrollmentAllChk.Checked)
            {
                SubmissionEnrollmentChk.Enabled = false;
                SendToEdgeEnrollmentChk.Enabled = false;
                GetEnrollmentEDGEChk.Enabled = false;
                CreateEnrollmentReportsChk.Enabled = false;
                SupplementalAllChk.Enabled = false;
                SendToEdgeSupplementalChk.Enabled = false;
                GetSupplementalEDGEChk.Enabled = false;
                CreateSupplementalReportsChk.Enabled = false;
                SubmissionSupplementalChk.Enabled = false;
            }
            else
            {
                SubmissionEnrollmentChk.Enabled = true;
                SendToEdgeEnrollmentChk.Enabled = true;
                GetEnrollmentEDGEChk.Enabled = true;
                SupplementalAllChk.Enabled = true;
                SendToEdgeSupplementalChk.Enabled = true;
                GetSupplementalEDGEChk.Enabled = true;
                CreateSupplementalReportsChk.Enabled = true;
                CreateEnrollmentReportsChk.Enabled = true;
                SubmissionSupplementalChk.Enabled = true;
            }
        }

        private void Gobtn_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            this.UseWaitCursor = true;
            this.Text = "Running...";

            String EDGEenvironment;

            if (radioButtonProd.Checked == true)
            {
                EDGEenvironment = "P";
            }
            else
            {
                EDGEenvironment = "T";
            }


            ProgramOptions opt = new ProgramOptions();

            if (SelectAllChk.Checked)
            {
                opt.RunAll(ReportYearTxtBx.Text);
            }
            else
            {
                opt.enrollmentReportCreate = SubmissionEnrollmentChk.Checked;
                opt.medClaimsReportCreate = SubmissionMedChk.Checked;
                opt.pharmClaimsReportCreate = SubmissionPharmChk.Checked;
                opt.enrollmentEdgeSubmit = SendToEdgeEnrollmentChk.Checked;
                opt.medClaimsEdgeSubmit = SendToEdgeMedChk.Checked;
                opt.pharmClaimsEdgeSubmit = SendToEdgePharmChk.Checked;
                opt.enrollmentGetServerOutput = GetEnrollmentEDGEChk.Checked;
                opt.medClaimsGetServerOutput = GetMedEDGEChk.Checked;
                opt.pharmClaimsGetServerOutput = GetPharmEDGEChk.Checked;
                opt.enrollmentOutputReportCreate = CreateEnrollmentReportsChk.Checked;
                opt.medClaimsOutputReportCreate = CreateMedReportsChk.Checked;
                opt.pharmClaimsOutputReportCreate = CreatePharmReportsChk.Checked;
                opt.CloseAfterCompletion = CloseAfterChk.Checked;
                opt.supplementalReportCreate = SubmissionSupplementalChk.Checked;
                opt.supplementalEdgeSubmit = SendToEdgeSupplementalChk.Checked;
                opt.supplementalGetServerOutput = GetSupplementalEDGEChk.Checked;
                opt.supplementalOutputReportCreate = CreateSupplementalReportsChk.Checked;
                opt.year = ReportYearTxtBx.Text;

            }


            IT_0363a.Control.FlowControl(origin, EDGEenvironment, opt);

            this.Text = "IT_0363 EDGE GUI";
            this.UseWaitCursor = false;
            this.Enabled = true;

        }

        private void MedAllChk_CheckedChanged(object sender, EventArgs e)
        {
            SubmissionMedChk.Checked = MedAllChk.Checked;
            SendToEdgeMedChk.Checked = MedAllChk.Checked;
            GetMedEDGEChk.Checked = MedAllChk.Checked;
            CreateMedReportsChk.Checked = MedAllChk.Checked;

            //disable other buttons because check all was selected
            if (MedAllChk.Checked)
            {
                SubmissionMedChk.Enabled = false;
                SendToEdgeMedChk.Enabled = false;
                GetMedEDGEChk.Enabled = false;
                CreateMedReportsChk.Enabled = false;
                SupplementalAllChk.Enabled = false;
                SendToEdgeSupplementalChk.Enabled = false;
                GetSupplementalEDGEChk.Enabled = false;
                CreateSupplementalReportsChk.Enabled = false;
                SubmissionSupplementalChk.Enabled = false;
            }
            else
            {
                SubmissionMedChk.Enabled = true;
                SendToEdgeMedChk.Enabled = true;
                GetMedEDGEChk.Enabled = true;
                CreateMedReportsChk.Enabled = true;
                SupplementalAllChk.Enabled = true;
                SendToEdgeSupplementalChk.Enabled = true;
                GetSupplementalEDGEChk.Enabled = true;
                CreateSupplementalReportsChk.Enabled = true;
                SubmissionSupplementalChk.Enabled = true;
            }
        }

        private void PharmAllChk_CheckedChanged(object sender, EventArgs e)
        {
            SubmissionPharmChk.Checked = PharmAllChk.Checked;
            SendToEdgePharmChk.Checked = PharmAllChk.Checked;
            GetPharmEDGEChk.Checked = PharmAllChk.Checked;
            CreatePharmReportsChk.Checked = PharmAllChk.Checked;

            //disable other buttons because check all was selected
            if (PharmAllChk.Checked)
            {
                SubmissionPharmChk.Enabled = false;
                SendToEdgePharmChk.Enabled = false;
                GetPharmEDGEChk.Enabled = false;
                CreatePharmReportsChk.Enabled = false;
                SupplementalAllChk.Enabled = false;
                SendToEdgeSupplementalChk.Enabled = false;
                GetSupplementalEDGEChk.Enabled = false;
                CreateSupplementalReportsChk.Enabled = false;
                SubmissionSupplementalChk.Enabled = false;
            }
            else
            {
                SubmissionPharmChk.Enabled = true;
                SendToEdgePharmChk.Enabled = true;
                GetPharmEDGEChk.Enabled = true;
                CreatePharmReportsChk.Enabled = true;
                SupplementalAllChk.Enabled = true;
                SendToEdgeSupplementalChk.Enabled = true;
                GetSupplementalEDGEChk.Enabled = true;
                CreateSupplementalReportsChk.Enabled = true;
                SubmissionSupplementalChk.Enabled = true;
            }
        }

        private void SubmissionAllChk_CheckedChanged(object sender, EventArgs e)
        {
            SubmissionEnrollmentChk.Checked = SubmissionAllChk.Checked;
            SubmissionMedChk.Checked = SubmissionAllChk.Checked;
            SubmissionPharmChk.Checked = SubmissionAllChk.Checked;

            //disable other buttons because check all was selected
            if (SubmissionAllChk.Checked)
            {
                SubmissionEnrollmentChk.Enabled = false;
                SubmissionMedChk.Enabled = false;
                SubmissionPharmChk.Enabled = false;
                SupplementalAllChk.Enabled = false;
                SendToEdgeSupplementalChk.Enabled = false;
                GetSupplementalEDGEChk.Enabled = false;
                CreateSupplementalReportsChk.Enabled = false;
                SubmissionSupplementalChk.Enabled = false;
            }
            else
            {
                SubmissionEnrollmentChk.Enabled = true;
                SubmissionMedChk.Enabled = true;
                SubmissionPharmChk.Enabled = true;
                SupplementalAllChk.Enabled = true;
                SendToEdgeSupplementalChk.Enabled = true;
                GetSupplementalEDGEChk.Enabled = true;
                CreateSupplementalReportsChk.Enabled = true;
                SubmissionSupplementalChk.Enabled = true;
            }

        }

        private void GetAllEDGEChk_CheckedChanged(object sender, EventArgs e)
        {
            GetEnrollmentEDGEChk.Checked = GetAllEDGEChk.Checked;
            GetMedEDGEChk.Checked = GetAllEDGEChk.Checked;
            GetPharmEDGEChk.Checked = GetAllEDGEChk.Checked;

            //disable other buttons because check all was selected
            if (GetAllEDGEChk.Checked)
            {
                GetEnrollmentEDGEChk.Enabled = false;
                GetMedEDGEChk.Enabled = false;
                GetPharmEDGEChk.Enabled = false;
                SupplementalAllChk.Enabled = false;
                SendToEdgeSupplementalChk.Enabled = false;
                GetSupplementalEDGEChk.Enabled = false;
                CreateSupplementalReportsChk.Enabled = false;
                SubmissionSupplementalChk.Enabled = false;
            }
            else
            {
                GetEnrollmentEDGEChk.Enabled = true;
                GetMedEDGEChk.Enabled = true;
                GetPharmEDGEChk.Enabled = true;
                SupplementalAllChk.Enabled = true;
                SendToEdgeSupplementalChk.Enabled = true;
                GetSupplementalEDGEChk.Enabled = true;
                CreateSupplementalReportsChk.Enabled = true;
                SubmissionSupplementalChk.Enabled = true;
            }
        }

        private void CreateAllReportsChk_CheckedChanged(object sender, EventArgs e)
        {
            CreateEnrollmentReportsChk.Checked = CreateAllReportsChk.Checked;
            CreateMedReportsChk.Checked = CreateAllReportsChk.Checked;
            CreatePharmReportsChk.Checked = CreateAllReportsChk.Checked;

            //disable other buttons because check all was selected
            if (CreateAllReportsChk.Checked)
            {
                CreateEnrollmentReportsChk.Enabled = false;
                CreateMedReportsChk.Enabled = false;
                CreatePharmReportsChk.Enabled = false;
                SupplementalAllChk.Enabled = false;
                SendToEdgeSupplementalChk.Enabled = false;
                GetSupplementalEDGEChk.Enabled = false;
                CreateSupplementalReportsChk.Enabled = false;
                SubmissionSupplementalChk.Enabled = false;
            }
            else
            {
                CreateEnrollmentReportsChk.Enabled = true;
                CreateMedReportsChk.Enabled = true;
                CreatePharmReportsChk.Enabled = true;
                SupplementalAllChk.Enabled = true;
                SendToEdgeSupplementalChk.Enabled = true;
                GetSupplementalEDGEChk.Enabled = true;
                CreateSupplementalReportsChk.Enabled = true;
                SubmissionSupplementalChk.Enabled = true;
            }
        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }

        private void SendToEdgeAllChk_CheckedChanged(object sender, EventArgs e)
        {
            SendToEdgeEnrollmentChk.Checked = SendToEdgeAllChk.Checked;
            SendToEdgeMedChk.Checked = SendToEdgeAllChk.Checked;
            SendToEdgePharmChk.Checked = SendToEdgeAllChk.Checked;

            //disable other buttons because check all was selected
            if (SendToEdgeAllChk.Checked)
            {
                SendToEdgeEnrollmentChk.Enabled = false;
                SendToEdgeMedChk.Enabled = false;
                SendToEdgePharmChk.Enabled = false;
                SupplementalAllChk.Enabled = false;
                SendToEdgeSupplementalChk.Enabled = false;
                GetSupplementalEDGEChk.Enabled = false;
                CreateSupplementalReportsChk.Enabled = false;
                SubmissionSupplementalChk.Enabled = false;
            }
            else
            {
                SendToEdgeEnrollmentChk.Enabled = true;
                SendToEdgeMedChk.Enabled = true;
                SendToEdgePharmChk.Enabled = true;
                SupplementalAllChk.Enabled = true;
                SendToEdgeSupplementalChk.Enabled = true;
                GetSupplementalEDGEChk.Enabled = true;
                CreateSupplementalReportsChk.Enabled = true;
                SubmissionSupplementalChk.Enabled = true;
            }
        }

        private void radioButtonProd_CheckedChanged(object sender, EventArgs e)
        {
            if(radioButtonProd.Checked)
            {
                MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                DialogResult result1;

                result1 = MessageBox.Show("You are choosing the EDGE production environement.  Are you sure you wish to do this?", "ALERT", buttons);

                if(result1 == DialogResult.Yes)
                {
                    radioButtonProd.Checked = true;
                }
                else
                {
                    radioButtonTest.Checked = true;
                }
            }
        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void SupplementalAllChk_CheckedChanged(object sender, EventArgs e)
        {
            if (SupplementalAllChk.Checked)
            {
                MessageBoxButtons buttons = MessageBoxButtons.YesNo;
                DialogResult result;

                result = MessageBox.Show("Supplemental submissions should be done alone, after initial claims submissions are complete. Would you like to continue?", "ALERT", buttons);

                if (result == DialogResult.Yes)
                {
                    SupplementalAllChk.Checked = true;
                    SubmissionEnrollmentChk.Checked = false;
                    SubmissionMedChk.Checked = false;
                    SubmissionPharmChk.Checked = false;
                    SendToEdgeEnrollmentChk.Checked = false;
                    SendToEdgeMedChk.Checked = false;
                    SendToEdgePharmChk.Checked = false;
                    GetEnrollmentEDGEChk.Checked = false;
                    GetMedEDGEChk.Checked = false;
                    GetPharmEDGEChk.Checked = false;
                    CreateEnrollmentReportsChk.Checked = false;
                    CreateMedReportsChk.Checked = false;
                    CreatePharmReportsChk.Checked = false;
                    SelectAllChk.Checked = false;
                    EnrollmentAllChk.Checked = false;
                    MedAllChk.Checked = false;
                    PharmAllChk.Checked = false;
                    SubmissionAllChk.Checked = false;
                    SendToEdgeAllChk.Checked = false;
                    GetAllEDGEChk.Checked = false;
                    CreateAllReportsChk.Checked = false;

                    SubmissionEnrollmentChk.Enabled = false;
                    SubmissionMedChk.Enabled = false;
                    SubmissionPharmChk.Enabled = false;
                    SendToEdgeEnrollmentChk.Enabled = false;
                    SendToEdgeMedChk.Enabled = false;
                    SendToEdgePharmChk.Enabled = false;
                    GetEnrollmentEDGEChk.Enabled = false;
                    GetMedEDGEChk.Enabled = false;
                    GetPharmEDGEChk.Enabled = false;
                    CreateEnrollmentReportsChk.Enabled = false;
                    CreateMedReportsChk.Enabled = false;
                    CreatePharmReportsChk.Enabled = false;
                    SelectAllChk.Enabled = false;
                    EnrollmentAllChk.Enabled = false;
                    MedAllChk.Enabled = false;
                    PharmAllChk.Enabled = false;
                    SubmissionAllChk.Enabled = false;
                    SendToEdgeAllChk.Enabled = false;
                    GetAllEDGEChk.Enabled = false;
                    CreateAllReportsChk.Enabled = false;
                    SubmissionSupplementalChk.Enabled = false;
                    SendToEdgeSupplementalChk.Enabled = false;
                    GetSupplementalEDGEChk.Enabled = false;
                    CreateSupplementalReportsChk.Enabled = false;

                }
                else
                {
                    SupplementalAllChk.Checked = false;
                }
            }
            else
            {
                SelectAllChk.Enabled = true;
                SubmissionEnrollmentChk.Enabled = true;
                SubmissionMedChk.Enabled = true;
                SubmissionPharmChk.Enabled = true;
                SendToEdgeEnrollmentChk.Enabled = true;
                SendToEdgeMedChk.Enabled = true;
                SendToEdgePharmChk.Enabled = true;
                GetEnrollmentEDGEChk.Enabled = true;
                GetMedEDGEChk.Enabled = true;
                GetPharmEDGEChk.Enabled = true;
                CreateEnrollmentReportsChk.Enabled = true;
                CreateMedReportsChk.Enabled = true;
                CreatePharmReportsChk.Enabled = true;
                EnrollmentAllChk.Enabled = true;
                MedAllChk.Enabled = true;
                PharmAllChk.Enabled = true;
                SubmissionAllChk.Enabled = true;
                SendToEdgeAllChk.Enabled = true;
                GetAllEDGEChk.Enabled = true;
                CreateAllReportsChk.Enabled = true;
                SubmissionSupplementalChk.Enabled = true;
                SendToEdgeSupplementalChk.Enabled = true;
                GetSupplementalEDGEChk.Enabled = true;
                CreateSupplementalReportsChk.Enabled = true;
            }
            SubmissionSupplementalChk.Checked = SupplementalAllChk.Checked;
            SendToEdgeSupplementalChk.Checked = SupplementalAllChk.Checked;
            GetSupplementalEDGEChk.Checked = SupplementalAllChk.Checked;
            CreateSupplementalReportsChk.Checked = SupplementalAllChk.Checked;
        }

        private void SubmissionSupplementalChk_CheckedChanged(object sender, EventArgs e)
        {
            if (SubmissionSupplementalChk.Checked && SendToEdgeSupplementalChk.Checked && GetSupplementalEDGEChk.Checked && CreateSupplementalReportsChk.Checked)
            {
                SupplementalAllChk.Checked = true;
            }
        }

        private void SendToEdgeSupplementalChk_CheckedChanged(object sender, EventArgs e)
        {
            if (SubmissionSupplementalChk.Checked && SendToEdgeSupplementalChk.Checked && GetSupplementalEDGEChk.Checked && CreateSupplementalReportsChk.Checked)
            {
                SupplementalAllChk.Checked = true;
            }
        }

        private void GetSupplementalEDGEChk_CheckedChanged(object sender, EventArgs e)
        {
            if (SubmissionSupplementalChk.Checked && SendToEdgeSupplementalChk.Checked && GetSupplementalEDGEChk.Checked && CreateSupplementalReportsChk.Checked)
            {
                SupplementalAllChk.Checked = true;
            }
        }

        private void CreateSupplementalReportsChk_CheckedChanged(object sender, EventArgs e)
        {
            if (SubmissionSupplementalChk.Checked && SendToEdgeSupplementalChk.Checked && GetSupplementalEDGEChk.Checked && CreateSupplementalReportsChk.Checked)
            {
                SupplementalAllChk.Checked = true;
            }
        }
    }
}
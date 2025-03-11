using PHPUtilities;
using System;

namespace PhpDriver
{
    class DA03761 : Logger, Php
    {
        public DA03761(LaunchRequest ProcId) : base(ProcId) { }

        public bool Initialize(string[] args)
        {
            string filename = $"CovenantHealthcare_Elig_{DateTime.Now:yyyyMMdd_HHmmss}.txt"; //TODO make this a MrData job once it supports parametric filenames. This single requirement is the only blocker.

            int sent = ExtractFactory.RunTextExtract(Data.AppNames.PhpWarehouse, query, "|", this, LoggerWorkDir + filename, AddHeaders: true);

            if (sent < 100) //1173 at time of authoring. 90+% decrease in membership seems unlikely.
            {
                this.WriteToLog($"Suspiciously small number of eligible members found: {sent}", UniversalLogger.LogCategory.ERROR);
                return false;
            }
            else
            {
                return true;
            }
        }

        static readonly string query = @"
                                        select
                                        'JD266'                                    AS [client_id],                            --1
                                        SubgroupId                                 AS [external_account_id],                  --2
                                        ClassId                                    AS [external_group_id],                    --3
                                        DM.SubscriberID                            AS [external_member_id],                   --4
                                        substring(DM.SubscriberSfx,11,12)          AS [person_code],                          --5                 
                                        case DM.MemberRelationship
                                            when 'M' then '1'
                                            when 'W' then '2' 
                                            when 'H' then '2'
                                            else '3'    
                                        End                                        AS [relationship_code],                    --6
                                        DM.MemberGender                            AS [gender_code],                          --7
                                        DM.EffectiveDateKey                        AS [effective_date],                       --8
                                        DM.TerminationDateKey                      AS [termination_date],                     --9
                                        DM.MemberFirstName                         AS [first_name],                           --10
                                        DM.MemberLastName                          AS [last_name],                            --11
                                        DM.MemberAddress1                          AS [address_line_1],                       --12
                                        DM.MemberAddress2                          AS [address_line_2],                       --13
                                        ''                                         AS [address_line_3],                       --14
                                        DM.MemberCity                              AS [city],                                 --15
                                        DM.MemberState                             AS [state],                                --16
                                        DM.MemberZip                               AS [zip_code],                             --17
                                        ''                                         AS [country],                              --18
                                        ''                                         AS [protected_address_indicator],          --19
                                        CONVERT(varchar,DM.MemberDateOfBirth, 112) AS [date_of_birth],                        --20
                                        DM.MemberPhone                             AS [phone_number],                         --21
                                        ''                                         AS [phone_number_qualifier],               --22
                                        DM.SubscriberSsn                           AS [social_security_number],               --23
                                        Case DM.FamilyIndicator
                                            When 'A' Then '03'  --Family
                                            When 'C' Then '01'  --Individual
                                            When '*' Then '03'  --Family Note: The asterisk is when there is a member level event.  This applies to members that are not the subscriber.   
                                            Else 'Unknown'
                                            End                                    AS [coverage_type],                        --24
                                        ''                                         AS [written_language],                     --25
                                        ''                                         AS [verbal_language],                      --26
                                        ''                                         AS [alternate_written_communication_mode], --27
                                        ''                                         AS [email],                                --28
                                        ''                                         AS [miscellaneous_id],                     --29
                                        ''                                         AS [coverage_status],                      --30
                                        ''                                         AS [date_of_death],                        --31
                                        ''                                         AS [handicap_indicator],                   --32
                                        ''                                         AS [encounter_member_id],                  --33
                                        ''                                         AS [new_enrollee_effective_date],          --34
                                        ''                                         AS [grace_period_indicator],               --35
                                        ''                                         AS [grace_period_effective_date],          --36
                                        ''                                         AS [udf_1_name],                           --37
                                        ''                                         AS [udf_1],                                --38
                                        ''                                         AS [udf_2_name],                           --39
                                        ''                                         AS [udf_2],                                --40
                                        ''                                         AS [udf_3_name],                           --41
                                        ''                                         AS [udf_3],                                --42
                                        ''                                         AS [udf_4_name],                           --43
                                        ''                                         AS [udf_4],                                --44
                                        ''                                         AS [udf_5_name],                           --45
                                        ''                                         AS [udf_5],                                --46
                                        ''                                         AS [udf_6_name],                           --47
                                        ''                                         AS [udf_6],                                --48
                                        ''                                         AS [udf_7_name],                           --49
                                        ''                                         AS [udf_7],                                --50
                                        ''                                         AS [udf_8_name],                           --51
                                        ''                                         AS [udf_8],                                --52
                                        ''                                         AS [udf_9_name],                           --53
                                        ''                                         AS [udf_9],                                --54
                                        ''                                         AS [udf_10_name],                          --55
                                        ''                                         AS [udf_10],                               --56
                                        ''                                         AS [udf_11_name],                          --57
                                        ''                                         AS [udf_11],                               --58
                                        ''                                         AS [udf_12_name],                          --59
                                        ''                                         AS [udf_12],                               --60
                                        ''                                         AS [udf_13_name],                          --61
                                        ''                                         AS [udf_13],                               --62
                                        ''                                         AS [udf_14_name],                          --63
                                        ''                                         AS [udf_14],                               --64
                                        ''                                         AS [udf_15_name],                          --65
                                        ''                                         AS [udf_15]                                --66
                                        from [PHP_Warehouse].[dbo].[vActiveAndFutureActiveMembers] AS DM
                                        where DM.GroupId = 'L0002237'

                                        Order by
                                        [client_id],
                                        [external_member_id],
                                        [person_code],
                                        [effective_date],
                                        [termination_date]";
    }
}

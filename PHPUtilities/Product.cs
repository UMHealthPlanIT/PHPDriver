using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utilities
{
    /// <summary>
    /// Contains product-specific accumulator details
    /// </summary>
    public class Product
    {
        private string FacetsPdPdId { get; set; }
        private Data DataSource { get; set; }
        /// <summary>
        /// The PDDS_MCTR_VAL1 field maintained by PHP to control how the accumulator is managed
        /// </summary>
        public string AccumulatorType { get; set; }
        /// <summary>
        /// The accumulator year that is active on the given AsOfDate
        /// </summary>
        public string AccumulatorYear { get; set; }
        /// <summary>
        /// Whether these accumulators roll over on a plan year or calendar year basis
        /// </summary>
        public Boolean PlanYear { get; set; }
        /// <summary>
        /// The month that the product's accumulator rolls over
        /// </summary>
        public int RenewalMonth { get; set; }
        public int RenewalDay { get; set; }
        private Logger _procLog { get; set; }

        /// <summary>
        /// Constructs the product object, which will contain the type of the product's accumulator and which year accumulator would be applied to based on the given date
        /// </summary>
        /// <param name="PDPD_ID">Facets product key</param>
        /// <param name="FacetsSource">Facets data source</param>
        /// <param name="AsOfDate">Used to populate the accumulator year property</param>
        public Product(String PDPD_ID, Data.AppNames FacetsSource, DateTime AsOfDate, Logger processLog)
        {
            FacetsPdPdId = PDPD_ID;
            DataSource = new Data(FacetsSource);
            AccumulatorType = GetAccumulatorTypeFromProduct();
            _procLog = processLog;
            AccumulatorYear = GetAccumulatorYear(AsOfDate);

        }

        private string GetAccumulatorTypeFromProduct()
        {
            List<String> AccumulatorTypeCode = ExtractFactory.ConnectAndQuery<string>(DataSource, String.Format(@"", FacetsPdPdId)).ToList();

            if (AccumulatorTypeCode.Count > 1)
            {
                throw new Exception("More than one product was found using the given product code " + FacetsPdPdId);
            }
            else
            {
                return AccumulatorTypeCode[0];
            }
        }

        public string AccumulatorYearQuery()
        {
            return string.Format(@"s", FacetsPdPdId);
        }

        private string GetAccumulatorYear(DateTime AsOfDate)
        {

            String PlanYearQuery = AccumulatorYearQuery();

            List<PlanRenewalConfiguration> planYear = ExtractFactory.ConnectAndQuery<PlanRenewalConfiguration>(_procLog.LoggerPhpConfig, PlanYearQuery).ToList(); //note: we're pointing to this database instead because this data element doesn't change often and it was a lag point.

            if (planYear.Count == 0) //if there are no records in this query, it means it is a calendar year plan
            {
                this.PlanYear = false;
                this.RenewalMonth = 1;
                this.RenewalDay = 1;
                return AsOfDate.Year.ToString();
            }
            else
            {
                this.PlanYear = true;
                string renewalMonth = planYear.First().RenewalMonth;
                this.RenewalMonth = Convert.ToInt32(renewalMonth);

                RenewalDay = (planYear.First().RenewalDay == string.Empty ? 1 : Convert.ToInt32(planYear.First().RenewalDay));

                DateTime renewalDate = new DateTime(DateTime.Today.Year, RenewalMonth, RenewalDay);

                if (AsOfDate >= renewalDate)
                {
                    return DateTime.Now.Year.ToString();

                }
                else
                {
                    return DateTime.Now.AddYears(-1).Year.ToString();
                }
            }
        }

        public class PlanRenewalConfiguration
        {
            public string RenewalType { get; set; }
            public string RenewalMonth { get; set; }

            public String RenewalDay { get; set; }
        }
    }
}

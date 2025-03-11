using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Driver
{
    public interface IPhp
    {
        /// <summary>
        /// For all programs, this is where control will be passed first by the Driver
        /// </summary>
        /// <returns>Completed successfully</returns>
        Boolean Initialize(string[] args);
        /// <summary>
        /// If an error is thrown in the initialize call, the Driver will pass control here
        /// </summary>
        /// <returns>Completed successfully</returns>
        void OnError(Exception exc);
        /// <summary>
        /// Once initialize returns true, the Driver will pass control here for clean-up
        /// </summary>
        /// <returns>Completed successfully</returns>
        void Finish();
    }
}

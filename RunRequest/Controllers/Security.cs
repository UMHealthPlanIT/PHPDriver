using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.DirectoryServices.AccountManagement;
using System.Web.Configuration;
using System.Web.Mvc;
using RunRequest.Models;


namespace RunRequest.Controllers
{
    public class Security
    {

        public static bool IsUserAllowed(string username)
        {
            PrincipalContext pc = new PrincipalContext((Environment.UserDomainName == Environment.MachineName ? ContextType.Machine : ContextType.Domain), Environment.UserDomainName);

            GroupPrincipal gp = GroupPrincipal.FindByIdentity(pc, "");
            UserPrincipal up = UserPrincipal.FindByIdentity(pc, username);
            return up.IsMemberOf(gp);
        }

        public static bool ScheduleSecurityCheck(string username, JobSchedule job)
        {
            bool userAllowed = false;

            PrincipalContext pc = new PrincipalContext((Environment.UserDomainName == Environment.MachineName ? ContextType.Machine : ContextType.Domain), Environment.UserDomainName);
            UserPrincipal up = UserPrincipal.FindByIdentity(pc, username);

            GroupPrincipal gp = GroupPrincipal.FindByIdentity(pc, "");

            if (up.IsMemberOf(gp)) //If user is a member of one of these groups they have access to all jobs
            {
                userAllowed = true;
            }

            return userAllowed;
        }
    }

    public class AdminAuthorization : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            string username = httpContext.User.Identity.Name;
            PrincipalContext pc = new PrincipalContext((Environment.UserDomainName == Environment.MachineName ? ContextType.Machine : ContextType.Domain), Environment.UserDomainName);

            GroupPrincipal gp = GroupPrincipal.FindByIdentity(pc, "");

            UserPrincipal up = UserPrincipal.FindByIdentity(pc, username);
            return up.IsMemberOf(gp);

        }

    }

        public class JobManagerAuthorizeAttribute : AuthorizeAttribute
    {

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            bool authorize = false;

            if (Security.IsUserAllowed(httpContext.User.Identity.Name))
            {
                authorize = true;
            }

            return authorize;
        }
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new HttpUnauthorizedResult();
        }
    }
}
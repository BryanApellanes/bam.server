using Bam.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bam.Server
{
    /// <summary>
    /// Used to specify the subdomain 
    /// a class should be served from when resolving
    /// hostname for a service
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ServiceSubdomainAttribute : Attribute
    {
        public ServiceSubdomainAttribute(string subdomain)
        {
            Subdomain = subdomain;
        }

        public string Subdomain { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj is ServiceSubdomainAttribute a)
            {
                return a.Subdomain.Equals(Subdomain);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Subdomain.ToSha1Int();
        }
    }
}
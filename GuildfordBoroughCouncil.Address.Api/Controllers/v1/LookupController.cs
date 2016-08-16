using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Description;
using System.Threading.Tasks;
using Location.Models;

namespace Location.Controllers
{
    /// <summary>
    /// Address lookup version 2.
    /// </summary>
    //[EnableCors("http://juba.guildford.gov.uk,http://wellington.guildford.gov.uk,http://localhost:62625", "*", "*")]
    [RoutePrefix("address/v1/lookup")]
    public class LookupController : ApiController
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="usrn"></param>
        /// <param name="postCode"></param>
        /// <param name="q"></param>
        /// <param name="geo"></param>
        /// <param name="classifications"></param>
        /// <param name="authorities"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        [HttpGet]
        [Route]
        [ResponseType(typeof(IEnumerable<GuildfordBoroughCouncil.Address.Models.Address>))]
        public async Task<IHttpActionResult> List(Int64? usrn = null, string postCode = null, string q = null, Geo near = null, List<string>? classifications = null, List<int>? authorities = null, List<string>? status = null)
        {
            if (usrn.HasValue)
            {
                return Ok(await Lookup.Data.ByUsrn(usrn.Value));
            }

            if (!String.IsNullOrWhiteSpace(postCode))
            {
                return Ok(await Lookup.Data.ByPostCode(postCode));
            }

            if (!String.IsNullOrWhiteSpace(q))
            {
                return Ok(await Lookup.Data.BySomething(q));
            }
            
            if (near.Latitude != null && near.Longitude != null && near.Radius != null)
            {
                return Ok(await Lookup.Data.FindNearest(near.Longitude, near.Latitude, near.Radius));
            }

            return BadRequest("You must specify a USRN, post code, query or location.");
        }

        /// <summary>
        /// Lookup an address by UPRN.
        /// </summary>
        /// <param name="Uprn">The UPRN to lookup</param>
        /// <returns>An address</returns>
        [HttpGet]
        [Route("{Uprn:long}")]
        [ResponseType(typeof(GuildfordBoroughCouncil.Address.Models.Address))]
        public async Task<IHttpActionResult> ByUprn(Int64 Uprn)
        {
            return Ok(await Lookup.Data.ByUprn(Uprn));
        }
    }
}

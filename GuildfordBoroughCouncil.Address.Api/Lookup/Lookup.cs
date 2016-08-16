﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using Location.SinglePoint;
using DotNetCoords;
using Location.Models;
using GuildfordBoroughCouncil.Linq;

using System.Threading.Tasks;
using System.Data.Entity;

using GuildfordBoroughCouncil.Address.Models;

namespace Location.Lookup
{
    public static class SearchResultItemExtension
    {
        private static string TryGetValue(this FieldInfo[] fi, string Tag)
        {
            var val = fi.Where(f => f.Tag == Tag).FirstOrDefault();

            if (val != null)
            {
                return val.Value;
            }

            return string.Empty;
        }

        public static GuildfordBoroughCouncil.Address.Models.Address ToAddress(this SearchResultItem result, AddressSearchScope Scope)
        {
            if (result != null)
            {
                var Address = new GuildfordBoroughCouncil.Address.Models.Address();

                string FormattedAddress;

                if (Scope == AddressSearchScope.Local)
                {
                    FormattedAddress = result.FieldItems.TryGetValue("FULL_ADDRESS");
                }
                else
                {
                    try
                    {
                        var DeliveryPointAddress = result.FieldItems.TryGetValue("DELIVERY_POINT_ADDRESS");

                        FormattedAddress = String.IsNullOrWhiteSpace(DeliveryPointAddress) ? result.TextToDisplay : DeliveryPointAddress;
                    }
                    catch
                    {
                        FormattedAddress = result.TextToDisplay;
                    }
                }

                var Index = Math.Max(0, FormattedAddress.IndexOf(result.FieldItems.TryGetValue("STREET") + ","));


                Address.Uprn = Int64.Parse(result.FieldItems.TryGetValue("UPRN") ?? null);
                Address.Usrn = Int64.Parse(result.FieldItems.TryGetValue("USRN") ?? null);
                Address.Organisation = result.FieldItems.TryGetValue("ORGANISATION").ToLower().ToTitleCase();
                Address.Property = (!String.IsNullOrWhiteSpace(FormattedAddress) ? FormattedAddress.Substring(0, Index).ToLower().ToTitleCase() : String.Empty).Trim().TrimEnd(',');
                Address.Street = result.FieldItems.TryGetValue("STREET").ToLower().ToTitleCase();
                Address.Locality = result.FieldItems.TryGetValue("LOCALITY").ToLower().ToTitleCase();
                Address.Town = result.FieldItems.TryGetValue("TOWN").ToLower().ToTitleCase();
                Address.County = result.FieldItems.TryGetValue("COUNTY").ToLower().ToTitleCase();
                Address.PostTown = result.FieldItems.TryGetValue("POSTTOWN");
                Address.PostCode = result.FieldItems.TryGetValue("POSTCODE");
                Address.Country = "UK";

                Index = FormattedAddress.LastIndexOf(",");

                if (!String.IsNullOrWhiteSpace(FormattedAddress))
                {
                    if (!String.IsNullOrWhiteSpace(Address.PostCode) && FormattedAddress.EndsWith(Address.PostCode, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Address.FullAddress = FormattedAddress.Substring(0, Index).ToLower().ToTitleCase() + FormattedAddress.Substring(Index + 1);
                    }
                    else
                    {
                        Address.FullAddress = FormattedAddress.ToLower().ToTitleCase();
                    }
                }
                else
                {
                    Address.FullAddress = Address.Street;
                }

                Address.Classification = result.FieldItems.TryGetValue("CLASSIFICATION");
                Address.Distance = result.FieldItems.TryGetValue("DISTANCE").Replace(" Meter", "m");

                Address.Northing = Double.Parse(result.FieldItems.TryGetValue("NORTHING"));
                Address.Easting = Double.Parse(result.FieldItems.TryGetValue("EASTING"));

                var LatLong = new OSRef(Address.Easting.Value, Address.Northing.Value).ToLatLng();
                LatLong.ToWGS84();
                Address.Latitude = LatLong.Latitude;
                Address.Longitude = LatLong.Longitude;

                //try
                //{
                //    using (var db = new Models.BluelightEntities())
                //    {
                //        var Blpu = db.BLPUs.Where(b => b.UPRN == Address.Uprn).SingleOrDefault();
                //        Address.AuthorityCode = Blpu.LOCAL_CUSTODIAN;
                //        Address.Authority = db.AUTHORITies.Where(a => a.AUTHORITY_REF == Address.AuthorityCode).SingleOrDefault().AUTHORITY_NAME.ToLower().ToTitleCase();
                //    }
                //}
                //catch
                //{
                //    if (!Address.AuthorityCode.HasValue && Scope == AddressSearchScope.Local)
                //    {
                //        Address.AuthorityCode = Properties.Settings.Default.ThisAuthorityCode;
                //        Address.Authority = Properties.Settings.Default.ThisAuthorityName;
                //    }
                //}

                switch(result.FieldItems.TryGetValue("LOGICAL_STATUS"))
                {
                    case "1":
                        Address.Status = AddressStatus.Active;
                        break;
                    case "3":
                        Address.Status = AddressStatus.Alternative;
                        break;
                    case "5":
                        Address.Status = AddressStatus.Candidate;
                        break;
                    case "8":
                        Address.Status = AddressStatus.Historic;
                        break;
                    case "6":
                        Address.Status = AddressStatus.Provisional;
                        break;
                    case "9":
                        Address.Status = AddressStatus.Rejected;
                        break;
                }

                return Address;
            }
            return null;
        }
    }

    public class AddressComparer : IEqualityComparer<Address>
    {
        public bool Equals(Address.Models.Address a1, Address.Models.Address a2)
        {
            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(a1, a2)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(a1, null) || Object.ReferenceEquals(a2, null))
                return false;

            return a1.Uprn == a2.Uprn;
        }

        public int GetHashCode(Address.Models.Address a)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(a, null)) return 0;

            //Get hash code for the Uprn field if it is not null.
            int hashUprn = a.Uprn == null ? 0 : a.Uprn.GetHashCode();

            //Calculate the hash code for the address.
            return hashUprn;
        }
    }

    public class Data
    {
        private static readonly System.ServiceModel.BasicHttpBinding BasicHttpBinding = new System.ServiceModel.BasicHttpBinding() { MaxReceivedMessageSize = 10485760 };
        private static readonly System.ServiceModel.EndpointAddress SinglePointEndpoint = new System.ServiceModel.EndpointAddress(Properties.Settings.Default.SinglePointUri);

        private static IEnumerable<int> LocalPlusSurrounding
        {
            get
            {
                return Properties.Settings.Default.LocalPlusSurroundingCodes.Cast<string>().Select(c => System.Convert.ToInt32(c));
            }
        }

        private static async Task<IEnumerable<Address>> AdvancedSearch(string SearchText, AddressSearchScope Scope = AddressSearchScope.National, string Category = "Residential")
        {
            SearchServiceSoapClient SPClient = new SearchServiceSoapClient(BasicHttpBinding, SinglePointEndpoint);
            
            #region Search Local

            SearchResultData Search = await SPClient.AdvancedSearchAsync("LLPG", SearchText);
            var ResultsScope = AddressSearchScope.Local;

            var Llpg = Search.Results.Items.Select(r => r.ToAddress(ResultsScope)).ToList();

            #endregion

            #region Search AddressBase

            IEnumerable<Address> AddressBase = new List<Address>();

            Search = await SPClient.AdvancedSearchAsync("AddressBase", SearchText);
            ResultsScope = Scope;

            AddressBase = Search.Results.Items.Select(r => r.ToAddress(ResultsScope))
                .WhereIf(ResultsScope == AddressSearchScope.LocalPlusSurrounding, r => LocalPlusSurrounding.Contains(r.AuthorityCode.Value))
                .WhereIf(ResultsScope == AddressSearchScope.Local, r => r.AuthorityCode == Properties.Settings.Default.ThisAuthorityCode).ToList();

            #endregion

            // Combine Llpg with AddressBase
            return Llpg.Union(AddressBase, new AddressComparer()).WhereIf(!String.IsNullOrWhiteSpace(Category), a => a.Classification.StartsWith(Category));
        }

        public static async Task<IEnumerable<Address>> ByUprn(Int64 Uprn)
        {
            return await AdvancedSearch("UPRN=" + Uprn.ToString());
        }

        public static async Task<IEnumerable<Address>> ByUsrn(Int64 Usrn)
        {
            return await AdvancedSearch("USRN=" + Usrn.ToString());
        }

        public static async Task<IEnumerable<GuildfordBoroughCouncil.Address.Models.Address>> ByPostCode(string PostCode, bool IncludeHistorical = false, AddressSearchScope Scope = AddressSearchScope.Local)
        {
            return await AdvancedSearch("POSTCODE=" + GuildfordBoroughCouncil.Address.PostCode.Format(PostCode) + ((IncludeHistorical) ? String.Empty : "|LOGICAL_STATUS=1"), Scope);
        }

        public static async Task<IEnumerable<GuildfordBoroughCouncil.Address.Models.Address>> BySomething(string Query, bool IncludeHistorical = false, AddressSearchScope Scope = AddressSearchScope.Local, string Category = "Residential")
        {
            var Filter = new List<AddressStatus>()
                {
                    AddressStatus.Active,
                    AddressStatus.Alternative
                };

            if (IncludeHistorical)
            {
                Filter.Add(AddressStatus.Historic);
            }

            SearchServiceSoapClient SPClient = new SearchServiceSoapClient(BasicHttpBinding, SinglePointEndpoint);

            #region Search Local

            SearchResultData Search = await SPClient.SearchAsync("LLPG", new QueryToken()
            {
                SearchText = new AnonymousSearchText()
                {
                    Value = Query
                },
                ReturnAllFields = true,

            });
            var ResultsScope = AddressSearchScope.Local;

            var Llpg = Search.Results.Items.Select(r => r.ToAddress(ResultsScope)).Where(r => Filter.Contains(r.Status)).ToList();

            #endregion

            #region Search AddressBase

            IEnumerable<Address.Models.Address> AddressBase = new List<Address.Models.Address>();

            Search = await SPClient.SearchAsync("AddressBase", new QueryToken()
                {
                    SearchText = new AnonymousSearchText()
                    {
                        Value = Query
                    },
                    ReturnAllFields = true,

                });

            ResultsScope = Scope;

            AddressBase = Search.Results.Items.Select(r => r.ToAddress(ResultsScope))
                .Where(r => Filter.Contains(r.Status))
                .WhereIf(ResultsScope == AddressSearchScope.LocalPlusSurrounding, r => LocalPlusSurrounding.Contains(r.AuthorityCode.Value))
                .WhereIf(ResultsScope == AddressSearchScope.Local, r => r.AuthorityCode == Properties.Settings.Default.ThisAuthorityCode).ToList();

            #endregion

            // Combine Llpg with AddressBase
            return Llpg.Union(AddressBase, new AddressComparer()).WhereIf(!String.IsNullOrWhiteSpace(Category), a => a.Classification.StartsWith(Category));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Long"></param>
        /// <param name="Lat"></param>
        /// <param name="Distance">Any positive value</param>
        /// <returns></returns>
        public static async Task<IEnumerable<GuildfordBoroughCouncil.Address.Models.Address>> FindNearest(double Long, double Lat, double Distance, string Category = "Residential")
        {
            var OS = new LatLng(Lat, Long).ToOSRef();

            SearchServiceSoapClient SPClient = new SearchServiceSoapClient(BasicHttpBinding, SinglePointEndpoint);

            #region Search Local

            SearchResultData Search = await SPClient.SpatialRadialSearchByEastingNorthingAsync("LLPG", OS.Easting.ToString(), OS.Northing.ToString(), "Meter", Distance.ToString());
            var ResultsScope = AddressSearchScope.Local;

            var Llpg = Search.Results.Items.Select(r => r.ToAddress(ResultsScope)).ToList();

            #endregion

            #region Search AddressBase

            IEnumerable<Address.Models.Address> AddressBase = new List<Address.Models.Address>();

            Search = await SPClient.SpatialRadialSearchByEastingNorthingAsync("AddressBase", OS.Easting.ToString(), OS.Northing.ToString(), "Meter", Distance.ToString());
            ResultsScope = Scope;

            AddressBase = Search.Results.Items.Select(r => r.ToAddress(ResultsScope))
                .WhereIf(ResultsScope == AddressSearchScope.LocalPlusSurrounding, r => LocalPlusSurrounding.Contains(r.AuthorityCode.Value))
                .WhereIf(ResultsScope == AddressSearchScope.Local, r => r.AuthorityCode == Properties.Settings.Default.ThisAuthorityCode).ToList();

            #endregion

            // Combine Llpg with AddressBase
            return Llpg.Union(AddressBase, new AddressComparer()).WhereIf(!String.IsNullOrWhiteSpace(Category), a => a.Classification.StartsWith(Category));
        }
    }
}
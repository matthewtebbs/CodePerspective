/*
 * 	MuddyTummy Core
 *
 * Copyright (c) 2010-2012 MuddyTummy Software, LLC
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

/* System */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace MuddyTummy.Cloud
{
    public sealed class GoogleAPIs
    {
        private static string ApiSensorArg { get { return "false"; } } /* TODO: QUERY VALUE FROM DEVICE: not using a sensor to determine the location */

        /*
         * Constants.
         */
        private static readonly Uri _uriDynamicMapLocal = new Uri(@"maps:", UriKind.Absolute);
        private static readonly Uri _uriDynamicMap = new Uri(@"https://maps.google.com/maps/", UriKind.Absolute);

        private static readonly Uri _uriMapsApis = new Uri(@"https://maps.googleapis.com/maps/api/");
        private static readonly Uri _uriStaticMapApi = new Uri(_uriMapsApis, @"staticmap");
        private static readonly Uri _uriPlaceDetailsApi = new Uri(_uriMapsApis, @"place/details/xml");
        private static readonly Uri _uriPlacesApi = new Uri(_uriMapsApis, @"place/search/xml");
        private static readonly Uri _uriGeocodingApi = new Uri(_uriMapsApis, @"geocode/xml");

        private const string _strYourGoogleAPIKey = @"<your Google API key goes here>";
        private const string _strYourAdMobPublisherID = @"<your AdMob Publisher ID goes here>";
        
        private const uint cmsecsWebTimeout = 30 * 1000; /* 30 seconds */

        public static string GoogleAPIKey { get { return _strYourGoogleAPIKey; } }
        public static string AdMobPublisherID { get { return _strYourAdMobPublisherID; } }
        
        /*
         * Enums.
         */
        public enum PlacesType {None = 0, Food, Drink, Parking};
            
        /*
         * 2D location expressed as a latitude and a longitude.
         */
        public class Location2D : ICloneable
        {
            /*
             * Member variables.
             */
            private double _latitude, _longitude;
            
            /*
             * Construction/destruction.
             */
            public Location2D()
            {
                _latitude = double.MinValue;
                _longitude = double.MinValue;
            }
            public Location2D(double latitude, double longitude)
            {
                _latitude = latitude;
                _longitude = longitude;
            }
            
            /*
             * Properties.
             */
            public static Location2D Empty {get {return new Location2D();}}
            
            public bool IsValid {get {return HasLatitude && HasLongitude;}}
            
            public bool HasLatitude {get {return double.MinValue != this.Latitude;}}
            public bool HasLongitude {get {return double.MinValue != this.Longitude;}}
        
            public double Latitude {get {return _latitude;}}
            public double Longitude {get {return _longitude;}}
            
            /*
             * Implement ICloneable.
             */
            public object Clone() {return this.MemberwiseClone();}
            
            /*
             * Implement Equal().
             */
            public override int GetHashCode ()
            {
                return _latitude.GetHashCode() ^ _longitude.GetHashCode();
            } 
            public override bool Equals(object objOther)
            {
                if (null == objOther || this.GetType() != objOther.GetType())
                    return false;
                
                return (objOther as Location2D).Latitude == this.Latitude && (objOther as Location2D).Longitude == this.Longitude;
            }
        }		
        
        /*
         * Place.
         */
        public class Place
        {
            public struct IdRef
            {
                public string Id {get; set;}
                public string Reference {get; set;}
            }
            
            /*
             * Member variables.
             */
            private IdRef _idref;
            private List<string> _types = new List<string>();
            
            /*
             * Construction/destruction.
             */
            public Place()
            {
                this.Id = this.Reference = string.Empty;
            }
            
            /*
             * Properties.
             */
            public Location2D Location2D {get; set;}
            
            public string Id {get {return _idref.Id;} set {_idref.Id = value;}}
            public string Reference {get {return _idref.Reference;} set {_idref.Reference = value;}}
            public IdRef IdRefTuple {get {return _idref;}}
            
            public List<string> Types {get {return _types;}}
            
            public string Name {get; set;}
            public string Vicinity {get; set;}
            public Uri IconUri {get; set;}
            
            public PlaceDistanceData DistanceFrom {get; set;}
            
            /*
             * Methods.
             */
            public void CalcPlaceDistanceFrom(Location2D location2dFrom)
            {
                double metres = MuddyTummy.GeoHelpers.DeltaMetresDistance(location2dFrom.Latitude, location2dFrom.Longitude,
                                                                          this.Location2D.Latitude, this.Location2D.Longitude);
                this.DistanceFrom = new PlaceDistanceData(metres, location2dFrom);
            }
        }
        
        /*
         * Place details.
         */
        public class PlaceDetails : Place
        {
            /*
             * Member variables.
             */
            public Address _address = new Address();
            
            /*
             * Construction/destruction.
             */
            public PlaceDetails()
            {
                this.Phone = this.IntlPhone = string.Empty;
                this.Rating = -1;
            }

            /*
             * Canonicalize a phone string.
             */
            private static string CanonicalizePhoneString(string strPhone)
            {
                if (string.IsNullOrWhiteSpace(strPhone))
                    return string.Empty;

                /*
                 * Take just our digits (whilst mapping to keypad).
                 */
                string strPhoneDigits = string.Empty;
                foreach (char ch in strPhone)
                {
                    char chLocal = ch;

                    if (char.IsUpper(chLocal))
                    {
                        int offset = chLocal - 'A';
                        if ('A' - 'A' <= offset && offset <= 'C' - 'A')
                            chLocal = '2';
                        else if ('D' - 'A' <= offset && offset <= 'F' - 'A')
                            chLocal = '3';
                        else if ('G' - 'A' <= offset && offset <= 'I' - 'A')
                            chLocal = '4';
                        else if ('J' - 'A' <= offset && offset <= 'L' - 'A')
                            chLocal = '5';
                        else if ('M' - 'A' <= offset && offset <= 'O' - 'A')
                            chLocal = '6';
                        else if ('P' - 'A' <= offset && offset <= 'S' - 'A')
                            chLocal = '7';
                        else if ('T' - 'A' <= offset && offset <= 'V' - 'A')
                            chLocal = '8';
                        else if ('W' - 'A' <= offset && offset <= 'Z' - 'A')
                            chLocal = '9';
                    }

                    if (chLocal == '-' || chLocal == ' ' || chLocal == '(' || chLocal == ')')
                        continue;

                    if (!char.IsDigit(chLocal))
                        return string.Empty;

                    strPhoneDigits += chLocal;
                }

                /*
                 * Now form the string to be of the form '1-###-###-####' (US only).
                 */
                Int64 numberPhone = 0;
                if (!Int64.TryParse(strPhoneDigits, out numberPhone))
                    return string.Empty;
                string strPhoneCanonicalized = string.Empty;
                if (strPhoneDigits.Length == 10)
                    strPhoneCanonicalized = string.Format("{0:1-###-###-####}", numberPhone);

                else if (strPhoneDigits.Length == 11 && '1' == strPhoneDigits[0])
                    strPhoneCanonicalized = string.Format("{0:#-###-###-####}", numberPhone);

                else
                    strPhoneCanonicalized = strPhoneDigits;

                return strPhoneCanonicalized;
            }

            /*
             * Properties.
             */
            public Address Address {get {return _address;}}
        
            public string Phone {get; set;}
            public Uri PhoneUri	{get {return new Uri(string.Format("tel:{1}", CanonicalizePhoneString(this.Phone)));}}
            public string IntlPhone {get; set;}
            public Uri IntlPhoneUri	{get {return new Uri(string.Format("tel:{1}", CanonicalizePhoneString(this.IntlPhone)));}}
            
            public bool HasRating {get {return -1 != this.Rating;}}
            public double Rating {get; set;}
            public Uri LinkUri {get; set;}
            public Uri WebsiteUri {get; set;}
        }
        
        /*
         * Place distance data.
         */
        public class PlaceDistanceData
        {
            private double _distanceMetres = 0.0f;
            private Location2D _location2dFrom;
            
            public PlaceDistanceData(double distanceMetres, Location2D location2dFrom)
            {
                _distanceMetres = distanceMetres;
                _location2dFrom = location2dFrom;
            }
            
            public double DistanceMiles {get {return MuddyTummy.GeoHelpers.ConvertFeetToMiles(MuddyTummy.GeoHelpers.ConvertMetresToFeet(_distanceMetres));}}
            public string Distance {get {return string.Format("{0:0.##}mi", this.DistanceMiles);}}
            
            public Location2D LocationFrom {get {return _location2dFrom;}}
        }
    
        /*
         * Address component.
         */
        public class AddressComponent
        {
            /*
             * Member variables.
             */
            private List<string> _types = new List<string>();
            
            /*
             * Properties.
             */
            public string LongName {get; set;}
            public string ShortName {get; set;}
            public List<string> Types {get {return _types;}}
        }
        
        /*
         * Address.
         */
        public class Address
        {
            /*
             * Constants.
             */
            private const string cstrStreetNumber = "street_number";
            private const string cstrRoute = "route";
            private const string cstrLocality = "locality";
            private const string cstrAdminAreaLvl1 = "administrative_area_level_1";
            private const string cstrPostalCode = "postal_code";
            private const string cstrCountry = "country";
            
            private const string cstrVicinity = "vicinity";
            
            private const string cstrAddress1 = "address1";
            private const string cstrAddress2 = "address2";
            
            private const string cstrFullAddress = "fulladdress";
            
            /*
             * Member variables.
             */
            private Dictionary<string, AddressComponent> _dictComponents = new Dictionary<string, AddressComponent>();
            
            /*
             * Properties.
             */
            public Dictionary<string, AddressComponent> Components {get {return _dictComponents;}}
            
            public bool IsValid
            {
                get
                {
                    foreach (KeyValuePair<string, AddressComponent> pair in _dictComponents)
                        if (!string.IsNullOrWhiteSpace(pair.Value.ShortName) || !string.IsNullOrWhiteSpace(pair.Value.LongName))
                            return true;
                    return false;
                }
            }
            
            public string StreetNumber	{get {return GetValue(cstrStreetNumber);} set {SetValue(cstrStreetNumber, value);}}
            public string Route			{get {return GetValue(cstrRoute);} set {SetValue(cstrRoute, value);}}
            public string Locality		{get {return GetValue(cstrLocality);} set {SetValue(cstrLocality, value);}}
            public string AdminAreaLvl1	{get {return GetValue(cstrAdminAreaLvl1);} set {SetValue(cstrAdminAreaLvl1, value);}}
            public string PostalCode	{get {return GetValue(cstrPostalCode);} set {SetValue(cstrPostalCode, value);}}
            public string Country		{get {return GetValue(cstrCountry);} set {SetValue(cstrCountry, value);}}

            public string Vicinity		{get {return GetValue(cstrVicinity);} set {SetValue(cstrVicinity, value);}}
            
            public string Address1		{get {return GetValue(cstrAddress1);} set {SetValue(cstrAddress1, value);}}
            public string Address2		{get {return GetValue(cstrAddress2);} set {SetValue(cstrAddress2, value);}}
            
            public string City			{get {return this.Locality;} set {this.Locality = value;}}
            public string State			{get {return this.AdminAreaLvl1;} set {this.AdminAreaLvl1 = value;}}
            
            public string LocalAddress	{get {return GetFormattedAddress(false /* local */);}}						
            public string WorldAddress	{get {return GetValue(cstrFullAddress) ?? GetFormattedAddress(true /* world */);} set {SetValue(cstrFullAddress, value);}}
            
            /*
             * Methods.
             */		
            private string GetValue(string strType, bool useShortName = false)
            {
                AddressComponent addresscomponent;
                if (!this.Components.TryGetValue(strType, out addresscomponent))
                    return null;
                return useShortName ? addresscomponent.ShortName : addresscomponent.LongName;
            }
            
            private void SetValue(string strType, string strValueLong, string strValueShort = null)
            {
                AddressComponent addresscomponent = new AddressComponent();
                addresscomponent.LongName = strValueLong;
                addresscomponent.ShortName = strValueShort ?? strValueLong;
                addresscomponent.Types.Add (strType);
                this.Components[strType] = addresscomponent;
            }

            private static void AppendStringWithPrefix(StringBuilder strb, string strPrefix, string strValue)
            {
                if (null == strb)
                    throw new ArgumentNullException();
                if (string.IsNullOrWhiteSpace(strValue))
                    return;
                strb.Append(strPrefix).Append(strValue);
            }

            private string GetFormattedAddress(bool doFull, bool doMultiLine = true)
            {
                StringBuilder strb = new StringBuilder();
                
                if (null != this.Address1)
                {
                    AppendStringWithPrefix(strb, string.Empty, this.Address1);
                    AppendStringWithPrefix(strb, doMultiLine ? "\n" : ", ", this.Address2);
                }
                else if (null != this.StreetNumber || null != this.Route)
                {
                    AppendStringWithPrefix(strb, string.Empty, this.StreetNumber);
                    AppendStringWithPrefix(strb, " ", this.Route);
                }
                else
                {
                    return this.Vicinity;
                }
                
                AppendStringWithPrefix(strb, doMultiLine ? "\n" : ", ", this.Locality);
                AppendStringWithPrefix(strb, ", ", this.AdminAreaLvl1);
                AppendStringWithPrefix(strb, " ", this.PostalCode);
                
                if (doFull)
                    AppendStringWithPrefix(strb, ", ", this.Country);
                
                return strb.ToString();
            }
        }
        
        /*
         * Places for a region.
         */
        public class RegionPlaces : IEnumerable<Place>
        {
            /*
             * Member variables.
             */
            private Location2D _location2d = null;
            private double _metresradius = 0.0f;
            
            private List<Place> _listplaces = null;
            private Place.IdRef[] _arrayIdRefs = null;
            
            /*
             * Construction/destruction.
             */
            public RegionPlaces(Location2D location2d, double metresradius, List<Place> listplaces)
            {
                _location2d = location2d;
                _metresradius = metresradius;
                _listplaces = listplaces;
                if (null != _listplaces)
                {
                    _arrayIdRefs = new Place.IdRef[_listplaces.Count];
                    uint index = 0;
                    foreach (Place place in _listplaces)
                        _arrayIdRefs[index++] = place.IdRefTuple;
                }	
            }
            
            /*
             * Properties.
             */
            public Location2D Location2d {get {return _location2d;}}
            public double MetresRadius {get {return _metresradius;}}		
            public int Count {get {return null != _listplaces ? _listplaces.Count : 0;}}	
            public Place.IdRef[] IdRefTuples {get {return _arrayIdRefs;}}
            
            /*
             * Methods.
             */
            public bool HasRegion(Location2D location2d, double metresradius)
            {
                return Location2D.Equals(location2d, this.Location2d) && metresradius == this.MetresRadius;
            }
            
            /*
             * Implementation of IEnumerable<Place>
             */
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {return GetEnumerator();}
            public IEnumerator<Place> GetEnumerator() {return null != _listplaces ? _listplaces.GetEnumerator() : (IEnumerator<Place>)null;}
        }
                
        /*
         * Parse XPath navigator nodes.
         */
        private static Location2D CreateLocation2DFromNode(XPathNavigator navNode)
        {
            double latitude = 0.0f, longitude = 0.0f;
            
            XPathNodeIterator iterator = navNode.Select("location/*") as XPathNodeIterator;				
            foreach (XPathNavigator navNodeLocal in iterator)
            {
                if (string.Equals(navNodeLocal.LocalName, "lat"))
                    latitude = navNodeLocal.ValueAsDouble;
            
                else if (string.Equals(navNodeLocal.LocalName, "lng"))
                    longitude = navNodeLocal.ValueAsDouble;
            }
            
            return new Location2D(latitude, longitude);
        }
        
        private static AddressComponent CreateAddressComponentFromNode(XPathNavigator navNode)
        {
            XPathNavigator navNodeLocal = navNode.Clone();
            if (!navNodeLocal.MoveToFirstChild()) return null;
            
            AddressComponent addresscomponent = new AddressComponent();
            
            do
            {
                switch (navNodeLocal.LocalName)
                {
                    case "long_name":
                        addresscomponent.LongName = navNodeLocal.Value;
                        break;
                        
                    case "short_name":
                        addresscomponent.ShortName = navNodeLocal.Value;
                        break;
                    
                    case "type":
                        addresscomponent.Types.Add(navNodeLocal.Value);
                        break;
                }
            }
            while (navNodeLocal.MoveToNext());
            
            return addresscomponent;
        }
        
        private static void HandlePlaceNode(Place place, XPathNavigator navNodeLocal)
        {
            switch (navNodeLocal.LocalName)
            {
                case "id":
                    place.Id = navNodeLocal.Value;
                    break;
            
                case "name":
                    place.Name = navNodeLocal.Value;
                    break;
                
                case "type":
                    place.Types.Add(navNodeLocal.Value);
                    break;

                case "vicinity":
                    place.Vicinity = navNodeLocal.Value;
                    break;

                case "icon":
                    place.IconUri = new Uri(navNodeLocal.Value);
                    break;
                
                case "reference":
                    place.Reference = navNodeLocal.Value;
                    break;
            
                case "geometry":
                    place.Location2D = CreateLocation2DFromNode(navNodeLocal);
                    break;
            }
        }
        
        private static Place CreatePlaceFromNode(XPathNavigator navNode)
        {
            XPathNavigator navNodeLocal = navNode.Clone();
            if (!navNodeLocal.MoveToFirstChild()) return null;
            
            Place place = new Place();
                            
            do
            {
                HandlePlaceNode(place, navNodeLocal);
            }
            while (navNodeLocal.MoveToNext());
            
            return place;
        }
        
        private static PlaceDetails CreatePlaceDetailsFromNode(XPathNavigator navNode)
        {
            XPathNavigator navNodeLocal = navNode.Clone();
            if (!navNodeLocal.MoveToFirstChild()) return null;
            
            PlaceDetails placedetails = new PlaceDetails();
            
            do
            {
                switch (navNodeLocal.LocalName)
                {
                    case "formatted_address":
                        placedetails.Address.WorldAddress = navNodeLocal.Value;
                        break;
                        
                    case "formatted_phone_number":
                        placedetails.Phone = navNodeLocal.Value;
                        break;
            
                    case "intl_phone_number":
                        placedetails.IntlPhone = navNodeLocal.Value;
                        break;
            
                    case "rating":
                        placedetails.Rating = navNodeLocal.ValueAsDouble / 5 /* 5 star scale normalized to 0 -> 1 */;
                        break;
                        
                    case "url":
                        placedetails.LinkUri = new Uri(navNodeLocal.Value);
                        break;
                        
                    case "website":
                        placedetails.WebsiteUri = new Uri(string.Format("http:{1}", navNodeLocal.Value));
                        break;
                        
                    case "vicinity": /* from places data */
                        placedetails.Address.Vicinity = navNodeLocal.Value;
                        HandlePlaceNode(placedetails as Place, navNodeLocal);
                        break;
                    
                    case "address_component":
                        AddressComponent addresscomponent = CreateAddressComponentFromNode(navNodeLocal);
                        if (addresscomponent.Types.Count > 0)
                            placedetails.Address.Components.Add(addresscomponent.Types[0], addresscomponent);
                        break;
                    
                    default:
                        HandlePlaceNode(placedetails as Place, navNodeLocal);
                        break;
                }
            }
            while (navNodeLocal.MoveToNext());
            
            return placedetails;
        }
        
        /*
         * Member Variables.
         */
        private static Dictionary<string, PlaceDetails> _placesCache = new Dictionary<string, PlaceDetails>();
        
        /*
         * Empty places local caches.
         */
        public static void EmptyLocalCache()
        {
            lock (_placesCache)
                _placesCache.Clear();
        }

        /*
         * HTTP request helper to return XPath navigator for returned UTF-8 XML document.
         */
        private async static Task<XPathNavigator> RequestAndNavToDocumentAsync(Uri uriCall)
        {
            HttpWebRequest webrequest = (HttpWebRequest)WebRequest.Create(uriCall);
            webrequest.Accept = "text/xml; charset=\"utf-8\"";
            webrequest.KeepAlive = true;
            webrequest.Timeout = (int)cmsecsWebTimeout;

            using (HttpWebResponse webresponse = await webrequest.GetResponseAsync() as HttpWebResponse)
            {
                if (HttpStatusCode.OK != webresponse.StatusCode)
                    return null;

                using (MemoryStream memstream = new MemoryStream(8192))
                {
                    Stream datastream = webresponse.GetResponseStream();
                    await datastream.CopyToAsync(memstream);
                    memstream.Position = 0;
                    datastream.Close();

                    return new XPathDocument(memstream).CreateNavigator();
                }
            }
        }

        /*
         * Request place details given a place reference (synchronous).
         */
        public async static Task<PlaceDetails> RequestPlaceDetailsAsync(Place.IdRef idref)
        {
            PlaceDetails placedetails = null;
            
            if (string.IsNullOrWhiteSpace(idref.Reference))
                throw new ArgumentNullException();
            
            lock (_placesCache)
                if (_placesCache.TryGetValue(idref.Id, out placedetails))
                    return placedetails;
            
            Uri uriPlaceDetailsCall = new Uri(_uriPlaceDetailsApi,
                                              string.Format("?reference={0}&sensor={1}&key={2}",
                                                            Uri.EscapeDataString(idref.Reference),
                                                            ApiSensorArg,
                                                            GoogleAPIs.GoogleAPIKey));
            
            XPathNavigator navDocument = await RequestAndNavToDocumentAsync(uriPlaceDetailsCall);
            if (null != navDocument)
            {				
                XPathNavigator navNode = navDocument.SelectSingleNode("/PlaceDetailsResponse/result") as XPathNavigator;
                if (null != navNode)
                {
                    placedetails = CreatePlaceDetailsFromNode(navNode);
                    
                    lock (_placesCache)
                        _placesCache[placedetails.Id] = placedetails;
                }
            }
            
            return placedetails;
        }
        
        /*
         * Request places details given a list of places (asynchronous).
         */
        public async static Task<List<PlaceDetails>> RequestBatchPlaceDetailsAsync(Place.IdRef[] arrayIdRefs)
        {
            List<PlaceDetails> listplacedetails = new List<PlaceDetails>(arrayIdRefs.Length /* default capacity */);	
            foreach (Place.IdRef idref in arrayIdRefs)
            {
                PlaceDetails placedetails = await RequestPlaceDetailsAsync(idref);
                if (null == placedetails)
                    return null;			
                listplacedetails.Add(placedetails);
            }
            
            return listplacedetails;
        }
    
        /*
         * Request places given a location (asynchronous).
         */
        public async static Task<RegionPlaces> RequestPlacesAsync(Location2D location2d, string strTypesOfPlace, double metresradius)
        {
            List<Place> listplaces = new List<Place>(32 /* default capacity */);
            
            Uri uriPlacesCall = new Uri(_uriPlacesApi,
                                        string.Format("?location={0},{1}&radius={2}&types={3}&sensor={4}&key={5}",
                                                      location2d.Latitude, location2d.Longitude,
                                                      metresradius, /* radius (m) */
                                                      strTypesOfPlace,
                                                      ApiSensorArg,
                                                      GoogleAPIs.GoogleAPIKey));
            
            XPathNavigator navDocument = await RequestAndNavToDocumentAsync(uriPlacesCall);
            if (null == navDocument)
                return null;
            
            RegionPlaces regionplaces = null;

            XPathNodeIterator iteratorPlaces = navDocument.Select("/PlaceSearchResponse/result") as XPathNodeIterator;
            if (null != iteratorPlaces)
            {
                foreach (XPathNavigator navNode in iteratorPlaces)
                {
                    Place place = CreatePlaceFromNode(navNode);
                    if (null == place)
                        throw new InvalidOperationException();
                    listplaces.Add(place);
                }
                
                regionplaces = new RegionPlaces(location2d, metresradius, listplaces);
            }
            
            return regionplaces;
        }
        
        /*
         * Geocode the address into a location (asynchronous).
         */
        public async static Task<Location2D> RequestGeocodingAsync(string strAddress)
        {
            Location2D location2d = null;
            
            if (string.IsNullOrWhiteSpace(strAddress))
                throw new ArgumentNullException();
            
            Uri uriGeocodeCall = new Uri(_uriGeocodingApi,
                                         string.Format("?address={0}&sensor={1}", /* no key required */
                                                       Uri.EscapeDataString(strAddress),
                                                       ApiSensorArg,
                                                       GoogleAPIs.GoogleAPIKey));
            
            XPathNavigator navDocument = await RequestAndNavToDocumentAsync(uriGeocodeCall);
            if (null != navDocument)
            {				
                XPathNavigator navNode = navDocument.SelectSingleNode("/GeocodeResponse/result[1]/geometry") as XPathNavigator;
                if (null != navNode)
                {
                    location2d = CreateLocation2DFromNode(navNode);
                }
            }
            
            return location2d;
        }
        
        /*
         * Search types for a type of place.
         */
        public static string PlacesSearchString(PlacesType placestype)
        {
            string strPlacesSearch = null;
            switch (placestype)
            {
                case PlacesType.Food:
                    strPlacesSearch = "restaurant|cafe|diner|fastfood|food";
                    break;
                case PlacesType.Drink:
                    strPlacesSearch = "bar|pub|coffee|drink";
                    break;
                case PlacesType.Parking:
                    strPlacesSearch = "parking";
                    break;
                case PlacesType.None:
                    break;	
                
                default:
                    throw new NotImplementedException();
            }
            
            return strPlacesSearch;
        }

        /*
         * Queries for enumerable of Places.
         * 
         * Search() - Search by regular expression.
         * SortedByDistance() - Sorted by distance.
         */
        public static IEnumerable<Place> Searched(IEnumerable enumerable, string strSearch = null)
        {
            Regex regexSearch = !string.IsNullOrEmpty(strSearch) ? new Regex(string.Format (@"\b{0}", Regex.Escape(strSearch)), RegexOptions.IgnoreCase) : null;
        
            if (enumerable is IEnumerable<GoogleAPIs.Place>)
            {
                return
                    (from pld in enumerable.Cast<GoogleAPIs.Place>()
                        where null == regexSearch || regexSearch.IsMatch(pld.Name)
                        select pld);
            }
            else
                throw new NotSupportedException();
        }

        public static IEnumerable<Place> SortedByDistance(IEnumerable enumerable)
        {
            if (enumerable is IEnumerable<GoogleAPIs.Place>)
            {
                return
                    (from pld in enumerable.Cast<GoogleAPIs.Place>()
                     where null != pld.DistanceFrom
                     select pld)
                    .OrderBy(pld => pld.DistanceFrom.DistanceMiles);
            }
            else
                throw new NotSupportedException();
        }
    }
}
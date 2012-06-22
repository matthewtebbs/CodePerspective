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

namespace MuddyTummy
{
	public static class GeoHelpers
	{
		/*
		 * Geo constants.
		 */
		private const double cRadPerDegree = Math.PI / 180.0;
		
		private const double cKmRadiusOfEarth = 6371.0; /* mean radius of Earth */
		private const double cKmPerArcDegree = cKmRadiusOfEarth * cRadPerDegree; /* assumes perfectly spherical Earth */
		
		private const double cMetresPerFoot = 0.3048;
		private const double cFeetPerMile = 5280.0;
		private const double cMetresPerKilometre = 1000.0;
		
		/*
		 * Conversion between latitude or longitude distance and metres.
		 */
		public static double DeltaMetresForLatitudeSpan(double latSpan)
		{
			return Math.Abs(latSpan) * cKmPerArcDegree * cMetresPerKilometre;
		}
		
		public static double DeltaMetresForLongitudeSpan(double lngSpan, double lat)
		{
			double cosLat = Math.Cos(lat * cRadPerDegree);
			return Math.Abs(cosLat) > double.Epsilon ? Math.Abs(lngSpan) * (cKmPerArcDegree * cMetresPerKilometre * cosLat) : 0.0;
		}
		
		public static double LatitudeSpanFromMetres(double metres)
		{
			return metres / (cKmPerArcDegree * cMetresPerKilometre);
		}
	
		public static double LongitudeSpanFromMetres(double metres, double lat)
		{
			double cosLat = Math.Cos(lat * cRadPerDegree);
			return Math.Abs(cosLat) > 1.0e-12 ? metres / (cKmPerArcDegree * cMetresPerKilometre * cosLat) : 0.0;
		}
		
		/*
		 * Distance betwen two geo points.
		 */
		public static double DeltaMetresDistance(double latA, double lngA, double latB, double lngB)
		{
			/*
			 * Using "spherical law of cosines" distance between two points
			 * in preference to the haversine formula is accuracy below 1m is not required.
			 * 
			 * cos(AOB) = cos(latA)cos(latB)cos(lngB-lngA)+sin(latA)sin(latB)
			 * distance = R * cos(AOB) where R is radius of Earth
			 */
			double cosAOB = Math.Cos(latA * cRadPerDegree) * Math.Cos(latB * cRadPerDegree) * Math.Cos((lngB - lngA) * cRadPerDegree) +
							Math.Sin(latA * cRadPerDegree) * Math.Sin(latB * cRadPerDegree);
			return cKmRadiusOfEarth * cMetresPerKilometre * Math.Acos(cosAOB); 
		}
		
		/*
		 * Radius of a geo region.
		 */
		public static double RadiusMetresDistance(double latDelta, double lngDelta, double lat)
		{
			double metresLatitudeDelta = DeltaMetresForLatitudeSpan(latDelta);
			double metresLongitudeDelta = DeltaMetresForLongitudeSpan(lngDelta, lat);
			return Math.Max(metresLatitudeDelta, metresLongitudeDelta) / 2.0f;
		}
		
		/*
		 * Conversion between various units of distance measurement.
		 */
		public static double ConvertMetresToKilometres(double metres)		{return metres / cMetresPerKilometre;}
		public static double ConvertKilometresToMetres(double kilometres)	{return kilometres * cMetresPerKilometre;} 
		public static double ConvertMetresToFeet(double metres)				{return metres / cMetresPerFoot;}
		public static double ConvertFeetToMetres(double feet)				{return feet * cMetresPerFoot;}
		public static double ConvertMilesToFeet(double miles)				{return miles * cFeetPerMile;}
		public static double ConvertFeetToMiles(double feet)				{return feet / cFeetPerMile;}
	}
}
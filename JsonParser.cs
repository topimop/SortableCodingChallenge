using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization; 

/*
Product
 {
  "product_name": String   // A unique id for the product
  "manufacturer": String
  "family": String         // optional grouping of products
  "model": String
  "announced-date": String // ISO-8601 formatted date string, e.g. 2011-04-28T19:00:00.000-05:00
} 
{"product_name":"Sony_Cyber-shot_DSC-W310","manufacturer":"Sony","model":"DSC-W310","family":"Cyber-shot","announced-date":"2010-01-06T19:00:00.000-05:00"}


Listing
 {
  "title": String         // description of product for sale
  "manufacturer":  String // who manufactures the product for sale
  "currency": String      // currency code, e.g. USD, CAD, GBP, etc.
  "price": String         // price, e.g. 19.99, 100.00
}
*/


namespace L3sEntityResolution
{

	public interface IJsonObject
	{
		List<string> Tokenize();
	}

	#region Product
	public class Product : IJsonObject
	{
		public string product_name	{ get; set; }
		public string manufacturer	{ get; set; }
		public string model { get; set; }
		public string family { get; set; }
		public string announced_date { get; set; }

		public string[] GetStrings()
		{
			string[] strs = new string[4];
			strs[0] = product_name;
			strs[1] = manufacturer;
			strs[2] = model;
			strs[3] = family;

			return strs;
		}

		public List<string> Tokenize()
		{
			var list = new List<string>();
			JsonParser.TokenizeJsonProperties(list, GetStrings());
			return list;
		}
	}
	#endregion

	#region Listing
	public class Listing : IJsonObject
	{
		public string title			{ get; set; }
		public string manufacturer	{ get; set; }
		public string currency		{ get; set; }
		public string price			{ get; set; }

		public string[] GetStrings()
		{
			string[] strs = new string[2];

			strs[0] = title;
			strs[1] = manufacturer;

			return strs;
		}

		public List<string> Tokenize()
		{
			var list = new List<string>();
			JsonParser.TokenizeJsonProperties(list, GetStrings());
			return list;
		}
	}
	#endregion

	#region Result
	public class Result
	{
		public string product_name {get; set;}
		public Listing[] listings;
	}
	#endregion

	#region JsonParser
	public class JsonParser
	{
		private JavaScriptSerializer ser;

		public JsonParser()
		{
			ser = new JavaScriptSerializer(); 
		}

		public object ParseProduct(string line)
		{
			string str = line.Replace("announced-date", "announced_date");
			Product product = ser.Deserialize<Product>(str); 
			return product;
		}

		public object ParseListing(string line)
		{
			Listing listing = ser.Deserialize<Listing>(line);
			return listing;
		}

		public string FormatResult(Result result)
		{
			string str = ser.Serialize(result);
			return str;
		}

		public static List<string> TokenizeJsonProperties(List<string> list, string[] strJsonProperties)
		{
			foreach (string s in strJsonProperties)
				if (!String.IsNullOrEmpty(s))
					foreach (string t in Tokenize(s))
						if (!list.Contains(t))
							list.Add(t);

			return list;
		}


		#region Tokenize
		public static string[] Tokenize(string s)
		{
			const string strSeparators = "_- ";
			char[] separators = strSeparators.ToCharArray();

			string[] tokens = s.Split(separators, StringSplitOptions.RemoveEmptyEntries);
			return tokens;
		}
		#endregion

	}
	#endregion
}

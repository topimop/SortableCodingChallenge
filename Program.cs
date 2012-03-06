using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;

using TokensDictValue = 
	System.Tuple
	<
		System.Collections.Generic.List<int>, System.Collections.Generic.List<int>
	>;

using SortedBlocksValue = 
	System.Tuple
	<
		float, string
	>;

using Matches = System.Collections.Generic.List<int>;

namespace L3sEntityResolution
{
	public class PlainBlockCompare : IComparer<SortedBlocksValue>
	{
		public int Compare(SortedBlocksValue x, SortedBlocksValue y)
		{
			// Descending order.
			if (x.Item1 < y.Item1) return 1;
			if (x.Item1 > y.Item1) return -1;
			return 0;
		}
	}

	class Program
	{
		#region Fields
		public enum JsonObjectType { Product, Listing };

		#region Constants
		public string pathProducts = "Data\\products.txt";
		public string pathListings = "Data\\listings.txt";
		public string pathResults  = "results.txt";

		const int numValuesInProductJson = 5;
		const int numValuesInListingsJson = 4;
		const string quote = "\"";
		const string comma = ",";
		const string colon = ":";
		readonly char[] arComma = comma.ToCharArray();
		readonly char[] arColon = colon.ToCharArray();
		#endregion

		public List<Product> listProducts = new List<Product>();
		public List<Listing> listListings = new List<Listing>();
		bool[] bProductMatched;
		bool[] bListingMatched;

		public JsonParser jsonParser = new JsonParser();

		public Dictionary<string, TokensDictValue> tokensDictionary;
		public SortedSet<SortedBlocksValue> setSortedBlocks;
		public List<Matches> listMatches;

		public float fRho = 0.005f;
		public float fMatchThreshold = 0.85f;
		public double dDhMax;

		int numEntityMatches = 0;

		#endregion

		#region Ctor
		public Program()
		{
			tokensDictionary = new Dictionary<string, TokensDictValue>();
			setSortedBlocks = new SortedSet<SortedBlocksValue>(new PlainBlockCompare());
		}
		#endregion

		#region Main
		static void Main(string[] args)
		{
			Program program = new Program();
			program.Run();
			Debug.WriteLine(program.numEntityMatches);
		}
		#endregion

		#region Run
		public void Run()
		{
			int i = -1;

			#region Init Product and Listing Lists from disk files.
			bool bOk = 
				ReadFile(pathProducts, numValuesInProductJson, JsonObjectType.Product) &&
				ReadFile(pathListings, numValuesInListingsJson, JsonObjectType.Listing);
			if (!bOk) return;
			#endregion

			#region Initialize Tokens Dictionary with tokens from both products and listings.
			// Products
			for (i = 0; i < listProducts.Count; i++)
			{
				Product product = listProducts[i];
				AddTokensToDictionary(product, JsonObjectType.Product, i);
			}

			// Listing
			for (i = 0; i < listListings.Count; i++)
			{
				Listing listing = listListings[i];
				AddTokensToDictionary(listing, JsonObjectType.Listing, i);
			}
			#endregion

			#region Init Matches List
			listMatches = new List<Matches>(listProducts.Count);
			for (i = 0; i < listProducts.Count; i++)
				listMatches.Add(new List<int>());
			#endregion

			#region Obtain intersection of products and listings
			// Remove Tokens that do not appear in both of product and listing.
			// The remaining list is the intersection.

			StringCollection toBeRemoved = new StringCollection();
			foreach (KeyValuePair<string, TokensDictValue> kvp in tokensDictionary)
			{
				string key = kvp.Key;
				var value = kvp.Value;
				if (value.Item1.Count == 0 || value.Item2.Count == 0)
					toBeRemoved.Add(key);
			}

			foreach (string key in toBeRemoved)
				tokensDictionary.Remove(key);
			#endregion

			#region Block Pruning
			int mMax = Math.Min(listProducts.Count, listListings.Count);
			float fPruneThreshold = fRho * mMax;

			toBeRemoved = new StringCollection();
			foreach (KeyValuePair<string, TokensDictValue> kvp in tokensDictionary)
			{
				string key = kvp.Key;
				var value = kvp.Value;
				int mMax_i = Math.Min(value.Item1.Count, value.Item2.Count);
				bool bKeep = mMax_i < fPruneThreshold;
				if (!bKeep)
					toBeRemoved.Add(key);
			}

			foreach (string key in toBeRemoved)
				tokensDictionary.Remove(key);
			#endregion

			#region Sort Blocks
			foreach (KeyValuePair<string, TokensDictValue> kvp in tokensDictionary)
			{
				string dictKey = kvp.Key;
				var dictValue = kvp.Value;

				float fMax = (float) Math.Max(dictValue.Item1.Count, dictValue.Item2.Count);
				float fPlainBlockUtil = 1.0f / fMax;

				setSortedBlocks.Add(new SortedBlocksValue(fPlainBlockUtil, dictKey));
			}
			#endregion

			#region Block Processing
			bProductMatched = new bool[listProducts.Count];
			bListingMatched = new bool[listListings.Count];

			double dMaxNumComparisions = listListings.Count * listListings.Count;
			double dExponent = Math.Log10(dMaxNumComparisions) / 2.0f;
			dDhMax = Math.Pow(10.0, dExponent);

			int idxBlock = 0;
			int lastBlockWithMatches = -1;
			foreach (SortedBlocksValue block in setSortedBlocks)
			{
				bool bMatchesFound;
				bool bContinue;
				ProcessBlock(block, out bMatchesFound, out bContinue);
				if (bMatchesFound) lastBlockWithMatches = idxBlock;
				idxBlock++;
				if (!bContinue)
					break;
			}

			Debug.WriteLine(
				"Last Block with Matches: " + (1+lastBlockWithMatches) + " / " + setSortedBlocks.Count);
			#endregion

			#region Output Results
			int lineCount = 0;
			using (StreamWriter resultsFile = new StreamWriter(pathResults))
				for (i = 0; i < listProducts.Count; i++)
				{
					if (!bProductMatched[i]) continue;
					Result result = new Result();
					Product product = listProducts[i];
					Matches matches = listMatches[i];
					result.product_name = product.product_name;
					result.listings = new Listing[matches.Count];

					int j = 0;
					foreach (int listingIdx in matches)
					{
						Listing listing = listListings[listingIdx];
						result.listings[j++] = listing;
					}

					string strJson = jsonParser.FormatResult(result);
					resultsFile.WriteLine(strJson);
					//Debug.WriteLine(strJson);
					lineCount++;
				}
			#endregion
			Console.WriteLine("Wrote " + lineCount + " lines");
		}
		#endregion

		#region ProcessBlock
		public void ProcessBlock(SortedBlocksValue block, out bool bMatchesFound, out bool bContinue)
		{
			bMatchesFound = false;
			bContinue = true;
				
			string key = block.Item2;
			TokensDictValue val = tokensDictionary[key];

			int compK = val.Item1.Count * val.Item2.Count; // # of comparisons for this blok

			foreach (int productIdx in val.Item1)
			{
				foreach (int listingIdx in val.Item2)
				{
					if (bListingMatched[listingIdx]) continue;

					Product product = listProducts[productIdx];
					Listing listing = listListings[listingIdx];
					var productTokens = product.Tokenize();
					var listingTokens = listing.Tokenize();
					bool bMatch = IsMatch(productTokens, listingTokens);
					if (bMatch)
					{
						bProductMatched[productIdx] = true;
						bListingMatched[listingIdx] = true;
						Matches matches = listMatches[productIdx];
						matches.Add(listingIdx);
						numEntityMatches++;

						#region Debugging
						Debug.WriteLine("---------------------------");
						Debug.Write(productIdx + "\t" + listingIdx);
						string blockInfo = String.Format(
							" ({0}: {1} = {2} / {3})", key, block.Item1, val.Item1.Count, val.Item2.Count);
						Debug.WriteLine(blockInfo);

						foreach (string s in product.GetStrings())
							Debug.WriteLine("\t" + s);
						Debug.WriteLine(">");
						foreach (string s in listing.GetStrings())
							Debug.WriteLine("\t" + s);
						#endregion
					}
				}
			}

			bMatchesFound = numEntityMatches > 0;
			if (!bMatchesFound) return;

			double dHk = compK / (double) numEntityMatches;
			bool bPurge = dDhMax < dHk;
			bContinue = !bPurge;
		}
		#endregion

		#region AddTokensToDictionary
		public void AddTokensToDictionary(IJsonObject iJsonObject, JsonObjectType jsonObjectType, int index)
		{
			List<string> tokens = iJsonObject.Tokenize();
			foreach (string s in tokens)
			{
				if (s == null) continue;

				TokensDictValue dictValue = null;
				string key = s.ToLower();
				if (tokensDictionary.ContainsKey(key))
					dictValue = tokensDictionary[key];
				else
				{
					var list1 = new List<int>();
					var list2 = new List<int>();
					dictValue = new TokensDictValue(list1, list2);
					tokensDictionary.Add(key, dictValue);
				}

				List<int> innerBlock = jsonObjectType == JsonObjectType.Product
					? dictValue.Item1
					: dictValue.Item2;

				if (!innerBlock.Contains(index))
					innerBlock.Add(index);	
			}

		}
		#endregion

		#region ReadFile
		public bool ReadFile(string path, int numExpectedValues, JsonObjectType jsonObjectType)
		{
			int lineCount = 0;

			try
			{
				// Create an instance of StreamReader to read from a file.
				// The using statement also closes the StreamReader.
				using (StreamReader sr = new StreamReader(path))
				{
					String line;
					// Read and display lines from the file until the end of
					// the file is reached.
					while ((line = sr.ReadLine()) != null)
					{
						Product product = null;
						Listing listing = null;
						if (jsonObjectType == JsonObjectType.Product)
						{
							product = (Product) jsonParser.ParseProduct(line);
							listProducts.Add(product);
						}
						else
						{
							listing = (Listing)jsonParser.ParseListing(line);
							listListings.Add(listing);
						}
						lineCount++;
					}
				}
			}
			catch (Exception e)
			{
				// Let the user know what went wrong.
				Console.WriteLine("Failed to open file " + path + ".");
				Console.WriteLine(e.Message);
				return false;
			}

			Console.WriteLine("Read " + lineCount + " lines.");
			return true;
		}
		#endregion 

		#region IsMatch
		public bool IsMatch(List<string> listX, List<string> listY)
		{
			int numAttributeMatches = 0;

			foreach (string x in listX)
				foreach (string y in listY)
					if (x.ToLower() == y.ToLower())
					{
						numAttributeMatches++;
						break;
					}
			float fMatchScoreX = (float)numAttributeMatches / listX.Count;
			float fMatchScoreY = (float)numAttributeMatches / listY.Count;

			//bool bMatch = fMatchScoreX > fMatchThreshold && fMatchScoreY > fMatchThreshold;
			bool bMatch = fMatchScoreX > fMatchThreshold || fMatchScoreY > fMatchThreshold;

			return bMatch;
			
		}
		#endregion
	}
}


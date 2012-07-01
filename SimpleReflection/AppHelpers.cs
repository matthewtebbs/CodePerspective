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
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MuddyTummy
{
	public static class ReflectionHelpers
	{
		public static TypeConverter GetConverter(Type type)
		{
			TypeConverter converter = null;
				
			/*
			 * Explicitly construct common type converters.
			 * 
			 * TypeDescriptor.ConvertFrom() uses reflection Invoke() to construct the converter
			 * and of course this means that some type converters may be optimized away by the linker
			 * if used by the build.
			 * 
			 * Another workaround is to specify '--linkskip=system' on the command-line.
			 */
			if (type.IsEnum)
				converter = new System.ComponentModel.EnumConverter(type);
				
			else if (typeof(System.Boolean) == type)
				converter = new System.ComponentModel.BooleanConverter();
			
			else if (typeof(System.Char) == type)
				converter = new System.ComponentModel.CharConverter();
			
			else if (typeof(System.DateTime) == type)
				converter = new System.ComponentModel.DateTimeConverter();
			
			else if (typeof(System.Double) == type)
				converter = new System.ComponentModel.DoubleConverter();
			
			else if (typeof(System.Int32) == type)
				converter = new System.ComponentModel.Int32Converter();
			
			else if (typeof(System.Single) == type)
				converter = new System.ComponentModel.SingleConverter();
			
			else if (typeof(System.UInt32) == type)
				converter = new System.ComponentModel.UInt32Converter();
			
			else if (typeof(System.Uri) == type)
				converter = new System.UriTypeConverter();
			
			else /* last chance */
				converter = TypeDescriptor.GetConverter(type);
			
			return converter;
				
		}
		
		private static object EvaluateMatchAsObject(Match match, string strFallback, object obj, Type typeExpected)
		{
			/*
			 * We need to evaluate to a type's value.
			 */
			object objResult = match.Success ? (null != obj ? EvaluateViaReflection(match.Groups[1].Value, obj) : null) : strFallback;
			
			/*
			 * If we don't have the final type we expected then convert.
			 */
			Type typeResult = null != objResult ? objResult.GetType() : null;
			if (null != typeResult && null != typeExpected && typeResult != typeExpected)
			{
				if (typeExpected.IsEnum && typeof(string) == typeResult)
					objResult = string.Join(",", (objResult as string).Split(new char[] {',', ' '}));
				
				TypeConverter converter = GetConverter(typeExpected);
				if (null == converter)
					throw new InvalidOperationException(string.Format("ReflectionHelpers.GetConverter() could not construct converter for type '{0}'.", typeExpected));
					                                    
				objResult = converter.ConvertFrom(objResult);
			}
			
			return objResult;
		}
			
		private readonly static string cstrEvalExpr = @"<%=\s([\w.]*)\s%>";
		
		public static bool IsExpression(string str)
		{
			return Regex.Match(str, cstrEvalExpr).Success;
		}
			
		public static object EvaluateExpressionAsObject(string str, object obj, Type typeExpected = null /* default is string */)
		{
			if (null == str)
				return null;
			
			if (null == typeExpected)
				typeExpected = typeof(string);
			
			object objResult = null;
			
			try
			{
				if (typeof(string) == typeExpected)
				{
					/*
					 * Replace multiple string evaluations within a string.
					 */
					objResult = Regex.Replace
					(
						str, cstrEvalExpr,
						(match) => {return EvaluateMatchAsObject(match, match.Value, obj, typeExpected) as string;}
					);
				}
				else
				{
					/*
					 * Evaluate directly to the expected type.
					 */
					Match match = Regex.Match(str, cstrEvalExpr);
					objResult = EvaluateMatchAsObject(match, str, obj, typeExpected);
				}
			}
			catch (Exception exception)
			{
				Console.WriteLine(exception);
			}
			
			return objResult;
		}
		
		public static object EvaluateViaReflection(string expr, object obj)
		{
			string[] methods = expr.Split (new char[] {'.'});
			
			Type typeTarget = null, typeResult = obj.GetType();
			
			object objTarget = null, objResult = obj;
			
			foreach (string method in methods)
			{
				typeTarget = typeResult;
				objTarget = objResult;
				
				MethodInfo bindingMethod = typeTarget.GetMethod("get_" + method); /* property accessors only */
				if (null == bindingMethod || null == objResult)
					return null;
				
				objResult = bindingMethod.Invoke(objTarget, null);
				typeResult = null != objResult ? objResult.GetType() : bindingMethod.ReturnType;
			}
				
			return objResult;
		}
	}
}
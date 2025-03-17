using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Reflection;

namespace Utilities
{
    /// <summary>
    /// This class is just used right now to deseralize xml obejects. But could be used later for serializing
    /// </summary>
    public class XMLTranslations
    {
        /// <summary>
        /// This class doesn't do much except the final conversion. 
        /// If you have XSD converted to a Class and a file you cna use this class to turn that XML to a object.
        /// To see how to auto generate the XSD Class Conversion go to the Y: folder of IT_272 documentention
        /// To see the next step of converting to a Table also go to IT_272
        /// </summary>
        /// <typeparam name="T">This type is the XSD converted type</typeparam>
        /// <param name="Filename">The file that will be deseralized. It has to match the XSD.</param>
        /// <returns>The deserlized objects</returns>
        public static T Deseril<T>(string Filename)
        {
            using (FileStream xmlStream = new FileStream(Filename, FileMode.Open))
            {
                using (XmlReader xmlReader = XmlReader.Create(xmlStream))
                {
                    Type test = typeof(T);
                    XmlSerializer serializer = new XmlSerializer(test);
                    return (T)serializer.Deserialize(xmlReader);
                }
            }
        }

        /// <summary>
        /// Serializes a given object to XML
        /// </summary>
        /// <typeparam name="T">Type of the object to serialize</typeparam>
        /// <param name="outputPath">Full path of the XML file to be written</param>
        /// <param name="obj">Populated object to serialize</param>
        public static void Seril<T>(string outputPath, T obj, Boolean TestMode)
        {

            XmlSerializer mySerializer = new System.Xml.Serialization.XmlSerializer(typeof(T));

            FileSystem.ReportYearDir(System.IO.Path.GetDirectoryName(outputPath));

            String runType = TestMode ? "T" : "P";

            TextWriter writer = new StreamWriter(outputPath);

            mySerializer.Serialize(writer, obj);
            writer.Close();
        }

        /// <summary>
        /// Get the scalar values for the given Nodes
        /// </summary>        
        /// <param name="">Return the values in List</param>
        /// <param name="obj">Populated object to serialize</param>
        public static List<T> pullScalarValuesfromXML<T>(string ID, string Year, XmlDocument xDoc)
        {
            List<T> results = new List<T>();
            Type test = typeof(T);
            T row = (T)Activator.CreateInstance(test);
            List<string> lstScalarValues = new List<string>();
            PropertyInfo[] props = test.GetProperties();
            foreach (PropertyInfo objProb in props)
            {
                if (objProb.Name == ID.Trim())
                {
                    objProb.SetValue(row, Convert.ChangeType(xDoc.GetElementsByTagName(ID).Item(0).InnerText, objProb.PropertyType));
                }
                if (objProb.Name == Year.Trim())
                {
                    if (xDoc.GetElementsByTagName(Year) != null)
                        objProb.SetValue(row, Convert.ChangeType(xDoc.GetElementsByTagName(Year).Item(0).InnerText, objProb.PropertyType));
                }
            }
            results.Add(row);
            return results;
        }

        /// <summary>
        /// Serializes a given object to XML
        /// </summary>
        /// <typeparam name="T">Type of the object to serialize</typeparam>
        /// <param name="outputPath">Full path of the XML file to be written</param>
        /// <param name="obj">Populated object to serialize</param>
        public static List<T> pulldtforNodesfromXML<T>(string PrimaryKey, string Key, bool hasChild, XmlDocument xDoc)
        {
            List<T> results = new List<T>();
            Type test = typeof(T);
            XmlNodeList nodes = xDoc.GetElementsByTagName(PrimaryKey);
            foreach (XmlNode xndNode in nodes)
            {
                int i = 0;
                T row = default(T);
                PropertyInfo[] props = test.GetProperties();
                if (Key.Trim() != string.Empty && hasChild == false)
                {
                    List<string> distinctNode = new List<string>();
                    foreach (XmlNode xNode in xndNode)
                    {
                        distinctNode.Add(xNode.LocalName);
                    }
                    if (distinctNode.Distinct().Count() == 1)
                    {
                        foreach (XmlNode xNode in xndNode)
                        {
                            foreach (PropertyInfo objProb in props)
                            {
                                if (objProb.Name == Key.Trim())
                                {
                                    row = (T)Activator.CreateInstance(test);
                                    if (xndNode.ParentNode[Key].InnerText != string.Empty)
                                        objProb.SetValue(row, Convert.ChangeType(xndNode.ParentNode[Key].InnerText, objProb.PropertyType));
                                }
                                if (objProb.Name == xNode.Name)
                                {
                                    if (xNode.InnerText != string.Empty)
                                        objProb.SetValue(row, Convert.ChangeType(xNode.InnerText, objProb.PropertyType));
                                }
                            }
                            results.Add(row);
                        }
                    }
                    else
                    {
                        foreach (XmlNode xNode in xndNode)
                        {
                            if (xndNode.ParentNode[Key] == null) //If XML has same node name in both Parent and Child then skip the Parent node.
                                break;
                            i = i + 1;
                            foreach (PropertyInfo objProb in props)
                            {
                                if (objProb.Name == Key.Trim() && i == 1)
                                {
                                    row = (T)Activator.CreateInstance(test);
                                    if (xndNode.ParentNode[Key].InnerText != string.Empty)
                                        objProb.SetValue(row, Convert.ChangeType(xndNode.ParentNode[Key].InnerText, objProb.PropertyType));
                                }
                                if (objProb.Name == xNode.Name)
                                {
                                    if (xNode.InnerText != string.Empty)
                                        objProb.SetValue(row, Convert.ChangeType(xNode.InnerText, objProb.PropertyType));
                                }
                            }
                            if (xndNode.ChildNodes.Count == i)
                                results.Add(row);
                        }
                    }
                }
                else if (Key.Trim() != string.Empty && hasChild == true)
                {
                    foreach (XmlNode xNode in xndNode)
                    {
                        if (Key.Trim() == xNode.LocalName)
                        {
                            row = (T)Activator.CreateInstance(test);
                            foreach (XmlNode cldNode in xNode)
                            {
                                foreach (PropertyInfo objProb in props)
                                {
                                    if (objProb.Name == cldNode.Name)
                                    {
                                        if (cldNode.InnerText != string.Empty)
                                            objProb.SetValue(row, Convert.ChangeType(cldNode.InnerText, objProb.PropertyType));
                                        break;
                                    }
                                }
                            }
                            results.Add(row);
                        }
                    }
                }
                else
                {
                    row = (T)Activator.CreateInstance(test);
                    foreach (XmlNode xNode in xndNode)
                    {
                        foreach (PropertyInfo objProb in props)
                        {
                            if (objProb.Name == xNode.Name)
                            {
                                if (xNode.InnerText != string.Empty)
                                    objProb.SetValue(row, Convert.ChangeType(xNode.InnerText, objProb.PropertyType));
                                break;
                            }
                        }
                    }
                    results.Add(row);
                }
            }
            return results;
        }

        /// <summary>
        /// Serializes a given object to XML
        /// </summary>
        /// <typeparam name="T">Type of the object to serialize</typeparam>
        /// <param name="PrimaryKey">PrimaryKey node in the XML</param>
        /// <param name="NodeKey">Key node in the XML</param>
        /// <param name="xDoc">XML object</param>
        public static List<T> pulldtforNodesfromSDXML<T>(string NodeKey, string PrimaryKey, XmlDocument xDoc)
        {
            List<T> results = new List<T>();
            Type XMLType = typeof(T);
            XmlNodeList nodes = xDoc.GetElementsByTagName(NodeKey);
            T row = default(T);
            PropertyInfo[] props = XMLType.GetProperties();
            if (NodeKey == "includedPlanProcessingResult")
            {
                foreach (XmlNode xndNode in nodes)
                {
                    row = (T)Activator.CreateInstance(XMLType);

                    foreach (PropertyInfo objProb in props)
                    {
                        if (objProb.Name == "issuerRecordIdentifier")
                            objProb.SetValue(row, Convert.ChangeType(xndNode.ParentNode.FirstChild.InnerText, objProb.PropertyType));
                        
                        if (objProb.Name == "issuerIdentifier")
                            objProb.SetValue(row, Convert.ChangeType(xndNode.ParentNode.FirstChild.NextSibling.InnerText, objProb.PropertyType));
                        
                        if (objProb.Name == "statusTypeCode")
                           objProb.SetValue(row, Convert.ChangeType(xndNode.ParentNode.FirstChild.NextSibling.NextSibling.FirstChild.InnerText, objProb.PropertyType));                        
                    }

                    foreach (XmlNode xNode in xndNode)
                    {
                        foreach (PropertyInfo objProb in props)
                        {
                            if (objProb.Name == xNode.LocalName)
                            {
                                if (xNode.InnerText != string.Empty)
                                    objProb.SetValue(row, Convert.ChangeType(xNode.InnerText, objProb.PropertyType));
                                break;
                            }
                            if (objProb.Name == "PlanstatusTypeCode" && xNode.LocalName == "classifyingProcessingStatusType")
                            {
                                if (xNode.FirstChild.InnerText != string.Empty)
                                    objProb.SetValue(row, Convert.ChangeType(xNode.FirstChild.InnerText, objProb.PropertyType));
                                break;
                            }
                        }
                    }
                    results.Add(row);
                }
            }
            else
            {
                foreach (XmlNode xndNode in nodes)
                {
                    row = (T)Activator.CreateInstance(XMLType);
                    foreach (XmlNode xNode in xndNode)
                    {
                        if (xNode.LocalName == "classifyingProcessingStatusType")
                        {
                            foreach (PropertyInfo objProb in props)
                            {
                                if (objProb.Name == xNode.FirstChild.LocalName)
                                {
                                    if (xNode.FirstChild.InnerText != string.Empty)
                                        objProb.SetValue(row, Convert.ChangeType(xNode.FirstChild.InnerText, objProb.PropertyType));
                                    break;
                                }
                            }
                        }
                        else
                        {
                            foreach (PropertyInfo objProb in props)
                            {
                                if (objProb.Name == xNode.Name)
                                {
                                    if (xNode.InnerText != string.Empty)
                                        objProb.SetValue(row, Convert.ChangeType(xNode.InnerText, objProb.PropertyType));
                                    break;
                                }
                            }
                        }
                    }
                    if (PrimaryKey != string.Empty)
                    {
                        foreach (PropertyInfo objProb in props)
                        {
                            if (objProb.Name == PrimaryKey)
                            {
                                if (xndNode.ParentNode[PrimaryKey]!= null && xndNode.ParentNode[PrimaryKey].InnerText != string.Empty)
                                    objProb.SetValue(row, Convert.ChangeType(xndNode.ParentNode[PrimaryKey].InnerText, objProb.PropertyType));
                                break;
                            }
                        }
                    }
                    if(xndNode.ParentNode[PrimaryKey] != null)
                        results.Add(row);
                }
            }
            return results;
        }
    }
}

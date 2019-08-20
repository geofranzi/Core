﻿using BExIS.Dlm.Entities.Data;
using BExIS.Dlm.Services.Data;
using BExIS.Dlm.Services.DataStructure;
using BExIS.Xml.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using Vaiona.Utils.Cfg;

namespace BExIS.IO.Transform.Output
{
    public class OutputDataManager
    {
        #region export prepare files

        public string GenerateAsciiFile(long id, string title, string mimeType, bool withUnits)
        {
            DatasetManager datasetManager = new DatasetManager();

            try
            {
                DatasetVersion datasetVersion = datasetManager.GetDatasetLatestVersion(id);
                long datasetVersionId = datasetVersion.Id;

                return GenerateAsciiFile(id, datasetVersionId, title, mimeType, withUnits);
            }
            finally
            {
                datasetManager.Dispose();
            }
        }

        public string GenerateAsciiFile(long id, long versionId, string title, string mimeType, bool withUnits)
        {
            DatasetManager datasetManager = new DatasetManager();
            DataStructureManager datasetStructureManager = new DataStructureManager();

            try
            {
                DatasetVersion datasetVersion = datasetManager.GetDatasetVersion(versionId);

                string contentDescriptorTitle = "";
                string ext = "";
                string nameExt = "";
                TextSeperator textSeperator = TextSeperator.semicolon;

                if (withUnits) nameExt = "WithUnits";

                switch (mimeType)
                {
                    case "text/csv":
                        {
                            contentDescriptorTitle = "generatedCSV" + nameExt;
                            ext = ".csv";
                            textSeperator = TextSeperator.semicolon;
                            break;
                        }
                    case "text/tsv":
                        {
                            contentDescriptorTitle = "generatedTSV" + nameExt;
                            ext = ".tsv";
                            textSeperator = TextSeperator.tab;
                            break;
                        }
                    default:
                        {
                            contentDescriptorTitle = "generatedTXT" + nameExt;
                            ext = ".txt";
                            textSeperator = TextSeperator.tab;
                            break;
                        }
                }

                AsciiWriter writer = new AsciiWriter(textSeperator);

                string path = "";

                //ascii allready exist
                if (datasetVersion.ContentDescriptors.Count(p => p.Name.Equals(contentDescriptorTitle) &&
                    p.URI.Contains(datasetVersion.Id.ToString())) > 0 &&
                    !withUnits)
                {
                    #region FileStream exist

                    ContentDescriptor contentdescriptor = datasetVersion.ContentDescriptors.Where(p => p.Name.Equals(contentDescriptorTitle)).FirstOrDefault();
                    path = Path.Combine(AppConfiguration.DataPath, contentdescriptor.URI);

                    if (FileHelper.FileExist(path))
                    {
                        return path;
                    }

                    #endregion FileStream exist
                }

                // not exist, needs to generated
                DataTable data = datasetManager.GetLatestDatasetVersionTuples(id);
                data.Strip();

                long datastuctureId = datasetVersion.Dataset.DataStructure.Id;

                path = createDownloadFile(id, datasetVersion.Id, datastuctureId, "data", ext, writer, null, withUnits);

                storeGeneratedFilePathToContentDiscriptor(id, datasetVersion, ext, withUnits);

                //add units if want
                string[] units = null;
                if (withUnits) units = getUnits(datastuctureId, null);

                writer.AddData(data, path, datastuctureId, units);

                return path;
            }
            finally
            {
                datasetManager.Dispose();
            }
        }

        public string GenerateAsciiFile(string ns, DataTable table, string title, string mimeType, long dataStructureId, bool withUnits = false)
        {
            string ext = "";
            TextSeperator textSeperator = TextSeperator.semicolon;

            switch (mimeType)
            {
                case "text/csv":
                    {
                        ext = ".csv";
                        textSeperator = TextSeperator.semicolon;
                        break;
                    }
                case "text/tsv":
                    {
                        ext = ".tsv";
                        textSeperator = TextSeperator.tab;
                        break;
                    }
                default:
                    {
                        ext = ".txt";
                        textSeperator = TextSeperator.tab;
                        break;
                    }
            }

            AsciiWriter writer = new AsciiWriter(textSeperator);

            // write to file
            // if there is already a file, replace it
            string path = createDownloadFile(ns, dataStructureId, title, ext, writer);

            string[] units = null;
            string[] columns = null;
            if (withUnits)
            {
                columns = getColumnNames(table);
                units = getUnits(dataStructureId, columns);
            }

            writer.AddData(table, path, dataStructureId, units);

            return path;
        }

        /// <summary>
        /// create an Excel file from the given Datatable
        /// </summary>
        /// <param name="ns"></param>
        /// <param name="table"></param>
        /// <param name="title"></param>
        /// <returns></returns>
        public string GenerateExcelFile(string ns, DataTable table, string title, long dsId, string ext = ".xlsm", bool withUnits = false)
        {
            ExcelWriter writer = new ExcelWriter();
            string path = createDownloadFile(ns, dsId, title, ext, writer);

            string[] units = null;
            string[] columns = getColumnNames(table);
            if (withUnits) units = getUnits(dsId, columns);

            writer.AddDataTuplesToFile(table, path, dsId, units);

            return path;
        }

        public string GenerateExcelFile(long id, string title, bool createAsTemplate, DataTable data = null, bool withUnits = false)
        {
            string mimeType = "";
            string ext = ".xlsx";
            string contentDescriptorTitle = "";

            if (createAsTemplate)
            {
                ext = ".xlsm";
                contentDescriptorTitle = "generated";
            }
            else
            {
                ext = ".xlsx";
                if (withUnits) contentDescriptorTitle = "generatedExcelWithUnits";
                else contentDescriptorTitle = "generatedExcel";
            }

            mimeType = MimeMapping.GetMimeMapping(ext);

            DatasetManager datasetManager = new DatasetManager();

            try
            {
                DatasetVersion datasetVersion = datasetManager.GetDatasetLatestVersion(id);
                ExcelWriter writer = new ExcelWriter(createAsTemplate);

                string path = "";

                //excel allready exist
                if (datasetVersion.ContentDescriptors.Count(p => p.Name.Equals(contentDescriptorTitle) && p.URI.Contains(datasetVersion.Id.ToString())) > 0 &&
                    data == null)
                {
                    #region FileStream exist

                    ContentDescriptor contentdescriptor =
                        datasetVersion.ContentDescriptors.Where(p => p.Name.Equals(contentDescriptorTitle))
                            .FirstOrDefault();
                    path = Path.Combine(AppConfiguration.DataPath, contentdescriptor.URI);

                    long version = datasetVersion.Id;
                    long versionNrGeneratedFile =
                        Convert.ToInt64(contentdescriptor.URI.Split('\\').Last().Split('_')[1]);

                    // check if FileStream exist
                    if (FileHelper.FileExist(path) && version == versionNrGeneratedFile)
                    {
                        return path;
                    }

                    #endregion FileStream exist
                }

                // not exist needs to generated

                #region FileStream not exist

                if (data == null)
                {
                    DatasetManager dm = new DatasetManager();
                    data = dm.GetLatestDatasetVersionTuples(id);
                    data.Strip();
                }

                long datastuctureId = datasetVersion.Dataset.DataStructure.Id;

                if (createAsTemplate)
                {
                    string[] columnNames = (from dc in data.Columns.Cast<DataColumn>()
                                            select dc.Caption).ToArray();

                    path = createDownloadFile(id, datasetVersion.Id, datastuctureId, "data", ext, writer, columnNames);
                    storeGeneratedFilePathToContentDiscriptor(id, datasetVersion, ext, false);
                    writer.AddData(data.Rows, path, datastuctureId);
                }
                else
                {
                    path = createDownloadFile(id, datasetVersion.Id, datastuctureId, "data", ext, writer, null, withUnits);

                    // the default data is without units, so store the path of the file if it was generated
                    storeGeneratedFilePathToContentDiscriptor(id, datasetVersion, ext, withUnits);

                    string[] units = null;
                    if (withUnits) units = getUnits(datastuctureId, null);

                    writer.AddData(data, path, datastuctureId, units);
                }

                return path;

                #endregion FileStream not exist
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                datasetManager.Dispose();
            }
        }

        private string createDownloadFile(long id, long datasetVersionOrderNo, long dataStructureId, string title, string ext, DataWriter writer, string[] columns = null, bool withUnits = false)
        {
            string addtionalFileNameExt = "";
            if (withUnits) addtionalFileNameExt = "WithUnits";

            string filename = "data" + addtionalFileNameExt;

            if (ext.Equals(".csv") || ext.Equals(".txt") || ext.Equals(".tsv"))
            {
                AsciiWriter asciiwriter = (AsciiWriter)writer;
                return asciiwriter.CreateFile(id, datasetVersionOrderNo, dataStructureId, filename, ext);
            }
            else
            if (ext.Equals(".xlsm"))
            {
                ExcelWriter excelwriter = (ExcelWriter)writer;
                excelwriter.VisibleColumns = columns;
                return excelwriter.CreateFile(id, datasetVersionOrderNo, dataStructureId, filename, ext);
            }
            else
            if (ext.Equals(".xlsx"))
            {
                ExcelWriter excelwriter = (ExcelWriter)writer;
                return excelwriter.CreateFile(id, datasetVersionOrderNo, dataStructureId, filename, ext);
            }

            return "";
        }

        /// <summary>
        /// create a new file of given extension
        /// ns will be used to seperate from other files created of individual datasets
        /// </summary>
        /// <param name="ns">namespace</param>
        /// <param name="title"></param>
        /// <param name="ext"></param>
        /// <param name="writer"></param>
        /// <returns></returns>
        private string createDownloadFile(string ns, long datastructureId, string title, string ext, DataWriter writer, string[] columns = null)
        {
            ExcelWriter excelwriter = null;
            switch (ext)
            {
                // text based files
                case ".csv":
                case ".txt":
                case ".tsv":
                    AsciiWriter asciiwriter = (AsciiWriter)writer;
                    return asciiwriter.CreateFile(ns, title, ext);

                // excel files
                case ".xlsx":
                    excelwriter = (ExcelWriter)writer;
                    return excelwriter.CreateFile(ns, datastructureId, title, ext);

                case ".xlsm":
                    excelwriter = (ExcelWriter)writer;
                    excelwriter.VisibleColumns = columns;
                    return excelwriter.CreateFile(ns, datastructureId, title, ext);

                // no valid extension given
                default:
                    return "";
            }
        }

        private void storeGeneratedFilePathToContentDiscriptor(long datasetId, DatasetVersion datasetVersion, string ext, bool withUnits)
        {
            DatasetManager dm = new DatasetManager();
            string nameExt = "";
            if (withUnits) nameExt = "withUnits";

            try
            {
                string name = "";
                string mimeType = "";

                if (ext.Contains("csv"))
                {
                    name = "generatedCSV" + nameExt;
                    mimeType = "text/csv";
                }

                if (ext.Contains("txt"))
                {
                    name = "generatedTXT" + nameExt;
                    mimeType = "text/plain";
                }

                if (ext.Contains("tsv"))
                {
                    name = "generatedTSV" + nameExt;
                    mimeType = "text/tsv";
                }

                if (ext.Contains("xlsm"))
                {
                    name = "generated";
                    mimeType = "application/xlsm";
                }

                if (ext.Contains("xlsx"))
                {
                    name = "generatedExcel" + nameExt;
                    mimeType = "application/xlsx";
                }

                // create the generated FileStream and determine its location
                string dynamicPath = IOHelper.GetDynamicStorePath(datasetId, datasetVersion.Id, "data", ext);
                //Register the generated data FileStream as a resource of the current dataset version
                //ContentDescriptor generatedDescriptor = new ContentDescriptor()
                //{
                //    OrderNo = 1,
                //    Name = name,
                //    MimeType = mimeType,
                //    URI = dynamicPath,
                //    DatasetVersion = datasetVersion,
                //};

                if (datasetVersion.ContentDescriptors.Count(p => p.Name.Equals(name)) > 0)
                {   // remove the one contentdesciptor
                    foreach (ContentDescriptor cd in datasetVersion.ContentDescriptors)
                    {
                        if (cd.Name == name)
                        {
                            cd.URI = dynamicPath;
                            dm.UpdateContentDescriptor(cd);
                        }
                    }
                }
                else
                {
                    // add current contentdesciptor to list
                    //datasetVersion.ContentDescriptors.Add(generatedDescriptor);
                    dm.CreateContentDescriptor(name, mimeType, dynamicPath, 1, datasetVersion);
                }

                //dm.EditDatasetVersion(datasetVersion, null, null, null);
            }
            finally
            {
                dm.Dispose();
            }
        }

        private string[] getUnits(long datastuctureId, string[] columns)
        {
            DataStructureManager datasetStructureManager = new DataStructureManager();

            List<string> units = new List<string>();

            try
            {
                var sds = datasetStructureManager.StructuredDataStructureRepo.Get(datastuctureId);

                if (sds != null)
                {
                    var varList = sds.Variables.ToList();
                    if (columns != null && columns.Count() > 0)
                        varList = varList.Where(v => columns.Contains(v.Label)).ToList();

                    varList.ForEach(v => units.Add(v.Unit.Abbreviation));
                }

                return units.ToArray();
            }
            finally
            {
                datasetStructureManager.Dispose();
            }
        }

        private string[] getColumnNames(DataTable table)
        {
            return table.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToArray();
        }

        #endregion export prepare files

        #region datatable

        public static DataTable ProjectionOnDataTable(DataTable dt, string[] visibleColumns)
        {
            List<DataColumn> columnTobeDeleted = new List<DataColumn>();
            foreach (DataColumn column in dt.Columns)
            {
                if (!visibleColumns.Contains(column.Caption.ToUpper()))
                {
                    columnTobeDeleted.Add(column);
                }
            }
            columnTobeDeleted.ForEach(p => dt.Columns.Remove(p));
            return dt;
        }

        public static DataTable SelectionOnDataTable(DataTable dt, string selection, bool useCaption = false)
        {
            //if selection contains variable like defind in the caption the datatable need to change
            if (useCaption)
            {
                foreach (DataColumn c in dt.Columns)
                {
                    var t = c.Caption;
                    c.ColumnName = t;
                }
            }

            DataTable newDt = dt.Clone();
            DataRow[] rows = dt.Select(selection);
            foreach (var row in rows)
            {
                newDt.ImportRow(row);
            }

            return newDt;
        }

        public static void ClearTempDirectory()
        {
            string path = Path.Combine(AppConfiguration.DataPath, "Datasets", "Temp");
            if (Directory.Exists(path))
            {
                System.IO.DirectoryInfo di = new DirectoryInfo(path);

                foreach (FileInfo file in di.EnumerateFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.EnumerateDirectories())
                {
                    dir.Delete(true);
                }
            }
        }

        #endregion datatable
    }
}
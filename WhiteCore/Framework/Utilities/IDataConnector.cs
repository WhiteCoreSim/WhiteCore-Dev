/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the WhiteCore-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using WhiteCore.Framework.Services;

namespace WhiteCore.Framework.Utilities
{
    /// <summary>
    ///     Connector that links WhiteCore IDataPlugins to a database backend
    /// </summary>
    public interface IDataConnector : IGenericData
    {
        /// <summary>
        ///     Name of the module
        /// </summary>
        string Identifier { get; }

        /// <summary>
        ///     Checks to see if table 'table' exists
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        bool TableExists(string table);

        /// <summary>
        ///     Creates a table with indices
        /// </summary>
        /// <param name="table"></param>
        /// <param name="columns"></param>
        /// <param name="indexDefinitions"></param>
        void CreateTable(string table, ColumnDefinition[] columns, IndexDefinition[] indexDefinitions);

        /// <summary>
        ///     Get the latest version of the database
        /// </summary>
        /// <returns></returns>
        Version GetWhiteCoreVersion(string migratorName);

        /// <summary>
        ///     Set the version of the database
        /// </summary>
        /// <param name="version"></param>
        /// <param name="MigrationName"></param>
        void WriteWhiteCoreVersion(Version version, string MigrationName);

        /// <summary>
        ///     copy tables
        /// </summary>
        /// <param name="sourceTableName"></param>
        /// <param name="destinationTableName"></param>
        /// <param name="columnDefinitions"></param>
        /// <param name="indexDefinitions"></param>
        void CopyTableToTable(string sourceTableName, string destinationTableName, ColumnDefinition[] columnDefinitions,
                              IndexDefinition[] indexDefinitions);

        /// <summary>
        ///     Check whether the data table exists and that the columns are correct
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columnDefinitions"></param>
        /// <param name="indexDefinitions"></param>
        /// <returns></returns>
        bool VerifyTableExists(string tableName, ColumnDefinition[] columnDefinitions,
                               IndexDefinition[] indexDefinitions);

        /// <summary>
        ///     Check whether the data table exists and that the columns are correct
        ///     Then create the table if it is not created
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columnDefinitions"></param>
        /// <param name="indexDefinitions"></param>
        /// <param name="renameColumns"></param>
        void EnsureTableExists(string tableName, ColumnDefinition[] columnDefinitions,
                               IndexDefinition[] indexDefinitions, Dictionary<string, string> renameColumns);

        /// <summary>
        ///     Rename the table from oldTableName to newTableName
        /// </summary>
        /// <param name="oldTableName"></param>
        /// <param name="newTableName"></param>
        void RenameTable(string oldTableName, string newTableName);

        /// <summary>
        ///     Drop a table
        /// </summary>
        /// <param name="tableName"></param>
        void DropTable(string tableName);
    }

    public enum DataManagerTechnology
    {
        SQLite,
        MySql,
        MSSQL2008,
        MSSQL7
    }

    public class SchemaDefinition
    {
        string m_name;

        /// <summary>
        ///     Name of schema
        /// </summary>
        public string Name
        {
            get { return m_name; }
        }

        ColumnDefinition[] m_columns;

        /// <summary>
        ///     Columns in schema
        /// </summary>
        public ColumnDefinition[] Columns
        {
            get { return m_columns; }
        }

        IndexDefinition[] m_indices;

        /// <summary>
        ///     Indices in schema
        /// </summary>
        public IndexDefinition[] Indices
        {
            get { return m_indices; }
        }

        /// <summary>
        ///     Defines a schema with no indices.
        /// </summary>
        /// <param name="schemaName">Name of schema</param>
        /// <param name="columns">Columns in schema</param>
        public SchemaDefinition(string schemaName, ColumnDefinition[] columns)
        {
            m_name = schemaName;
            m_columns = columns;
            m_indices = new IndexDefinition[0];
        }

        /// <summary>
        ///     Defines a schema with indices
        /// </summary>
        /// <param name="schemaName">Name of schema</param>
        /// <param name="columns">Columns in schema</param>
        /// <param name="indices">Indices in schema</param>
        public SchemaDefinition(string schemaName, ColumnDefinition[] columns, IndexDefinition[] indices)
        {
            m_name = schemaName;
            m_columns = columns;
            m_indices = indices;
        }
    }

    public enum ColumnTypes
    {
        Blob,
        LongBlob,
        Char40,
        Char39,
        Char38,
        Char37,
        Char36,
        Char35,
        Char34,
        Char33,
        Char32,
        Char5,
        Char1,
        Char2,
        Date,
        DateTime,
        Double,
        Integer11,
        Integer30,
        UInteger11,
        UInteger30,
        String,        
        String10,
        String16,
        String30,
        String32,
        String36,
        String45,
        String50,
        String64,
        String128,
        String100,
        String255,
        String512,
        String1024,
        String8196,
        Text,
        MediumText,
        LongText,
        TinyInt1,
        TinyInt4,
        UTinyInt4,
        Float,
        Binary32,
        Binary64,
        UUID,
        Unknown        
    }

    public enum ColumnType
    {
        Blob,
        LongBlob,
        Char,
        Date,
        DateTime,
        Double,
        Integer,
        String,
        Text,
        MediumText,
        LongText,
        TinyInt,
        Float,
        Boolean,
        UUID,
        Binary,
        Unknown        
    }

    public class ColumnTypeDef
    {
        #region Const Types

        public static readonly ColumnTypeDef Blob = new ColumnTypeDef(ColumnType.Blob);
        public static readonly ColumnTypeDef Char32 = new ColumnTypeDef(ColumnType.Char, 32);
        public static readonly ColumnTypeDef Char33 = new ColumnTypeDef(ColumnType.Char, 33);
        public static readonly ColumnTypeDef Char34 = new ColumnTypeDef(ColumnType.Char, 34);
        public static readonly ColumnTypeDef Char35 = new ColumnTypeDef(ColumnType.Char, 35);
        public static readonly ColumnTypeDef Char36 = new ColumnTypeDef(ColumnType.Char, 36);
        public static readonly ColumnTypeDef Char37 = new ColumnTypeDef(ColumnType.Char, 37);
        public static readonly ColumnTypeDef Char38 = new ColumnTypeDef(ColumnType.Char, 38);
        public static readonly ColumnTypeDef Char39 = new ColumnTypeDef(ColumnType.Char, 39);
        public static readonly ColumnTypeDef Char40 = new ColumnTypeDef(ColumnType.Char, 40);
        public static readonly ColumnTypeDef Char5 = new ColumnTypeDef(ColumnType.Char, 5);
        public static readonly ColumnTypeDef Char1 = new ColumnTypeDef(ColumnType.Char, 1);
        public static readonly ColumnTypeDef Char2 = new ColumnTypeDef(ColumnType.Char, 2);
        public static readonly ColumnTypeDef Date = new ColumnTypeDef(ColumnType.Date);
        public static readonly ColumnTypeDef DateTime = new ColumnTypeDef(ColumnType.DateTime);
        public static readonly ColumnTypeDef Double = new ColumnTypeDef(ColumnType.Double);
        public static readonly ColumnTypeDef Float = new ColumnTypeDef(ColumnType.Float);
        public static readonly ColumnTypeDef Integer11 = new ColumnTypeDef(ColumnType.Integer, 11);
        public static readonly ColumnTypeDef Integer30 = new ColumnTypeDef(ColumnType.Integer, 30);
        public static readonly ColumnTypeDef UInteger11 = new ColumnTypeDef(ColumnType.Integer, 11, true);
        public static readonly ColumnTypeDef UInteger30 = new ColumnTypeDef(ColumnType.Integer, 30, true);
        public static readonly ColumnTypeDef LongBlob = new ColumnTypeDef(ColumnType.LongBlob);
        public static readonly ColumnTypeDef LongText = new ColumnTypeDef(ColumnType.LongText);
        public static readonly ColumnTypeDef MediumText = new ColumnTypeDef(ColumnType.MediumText);
        public static readonly ColumnTypeDef Text = new ColumnTypeDef(ColumnType.Text);
        public static readonly ColumnTypeDef String10 = new ColumnTypeDef(ColumnType.String, 10);
        public static readonly ColumnTypeDef String100 = new ColumnTypeDef(ColumnType.String, 100);
        public static readonly ColumnTypeDef String1024 = new ColumnTypeDef(ColumnType.String, 1024);
        public static readonly ColumnTypeDef String128 = new ColumnTypeDef(ColumnType.String, 128);
        public static readonly ColumnTypeDef String16 = new ColumnTypeDef(ColumnType.String, 16);
        public static readonly ColumnTypeDef String255 = new ColumnTypeDef(ColumnType.String, 255);
        public static readonly ColumnTypeDef String30 = new ColumnTypeDef(ColumnType.String, 30);
        public static readonly ColumnTypeDef String32 = new ColumnTypeDef(ColumnType.String, 32);
        public static readonly ColumnTypeDef String36 = new ColumnTypeDef(ColumnType.String, 36);
        public static readonly ColumnTypeDef String45 = new ColumnTypeDef(ColumnType.String, 45);
        public static readonly ColumnTypeDef String50 = new ColumnTypeDef(ColumnType.String, 50);
        public static readonly ColumnTypeDef String512 = new ColumnTypeDef(ColumnType.String, 512);
        public static readonly ColumnTypeDef String64 = new ColumnTypeDef(ColumnType.String, 64);
        public static readonly ColumnTypeDef String8196 = new ColumnTypeDef(ColumnType.String, 8196);
        public static readonly ColumnTypeDef TinyInt1 = new ColumnTypeDef(ColumnType.TinyInt, 1);
        public static readonly ColumnTypeDef TinyInt4 = new ColumnTypeDef(ColumnType.TinyInt, 4);
        public static readonly ColumnTypeDef UTinyInt4 = new ColumnTypeDef(ColumnType.TinyInt, 4, true);
        public static readonly ColumnTypeDef Binary32 = new ColumnTypeDef(ColumnType.Binary, 32);
        public static readonly ColumnTypeDef Binary64 = new ColumnTypeDef(ColumnType.Binary, 64);
        public static readonly ColumnTypeDef UUID = new ColumnTypeDef(ColumnType.UUID, 36);
        public static readonly ColumnTypeDef Unknown = new ColumnTypeDef(ColumnType.Unknown);


        

        #endregion

        public ColumnType Type { get; set; }
        public uint Size { get; set; }
        public string defaultValue { get; set; }
        public bool isNull { get; set; }
        public bool unsigned { get; set; }
        public bool auto_increment { get; set; }

        public ColumnTypeDef() { }
        public ColumnTypeDef(ColumnType type) { Type = type; }
        public ColumnTypeDef(ColumnType type, uint size) { Type = type; Size = size; }

        public ColumnTypeDef(ColumnType type, uint size, bool isunsigned) { Type = type; Size = size; unsigned = isunsigned; }

        public override bool Equals(object obj)
        {
            ColumnTypeDef foo = obj as ColumnTypeDef;
            return (foo != null && foo.Type.ToString() == Type.ToString() && foo.Size == Size &&
                    foo.defaultValue == defaultValue && foo.isNull == isNull && foo.unsigned == unsigned &&
                    foo.auto_increment == auto_increment);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class ColumnDefinition
    {
        public string Name { get; set; }
        public ColumnTypeDef Type { get; set; }

        public override bool Equals(object obj)
        {
            var cdef = obj as ColumnDefinition;
            if (cdef != null)
            {
                return cdef.Name == Name && cdef.Type == Type;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public enum IndexType
    {
        Primary,
        Index,
        Unique
    }

    public class IndexDefinition
    {
        public string[] Fields { get; set; }
        public IndexType Type { get; set; }
        public int IndexSize { get; set; }

        public override bool Equals(object obj)
        {
            var idef = obj as IndexDefinition;
            if (idef != null && idef.Type == Type && idef.Fields.Length == Fields.Length)
            {
                uint i = 0;
                return idef.Fields.All(field => field == Fields[i++]);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

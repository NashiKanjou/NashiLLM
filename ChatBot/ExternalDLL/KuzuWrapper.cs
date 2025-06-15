using System.Runtime.InteropServices;
/*
 *  This file is part of ArsCore.
 *
 *  ArsCore is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  ArsCore is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with ArsCore.  If not, see <https://www.gnu.org/licenses/>.
 */
namespace KuzuClient
{
    public class KuzuConnection : IDisposable
    {
        private _KuzuDatabase _db;
        private _KuzuConnection _conn;
        public KuzuConnection(string dbPath)
        {
            var config = Native.kuzu_default_system_config();
            int dbInitState = Native.kuzu_database_init(dbPath, config, out _db);
            if (dbInitState != 0)
                throw new Exception("Failed to init database.");

            int connInitState = Native.kuzu_connection_init(ref _db, out _conn);
            if (connInitState != 0)
                throw new Exception("Failed to init connection.");
        }
        public class KuzuQueryResultHandle : IDisposable
        {
            private _KuzuQueryResult _result;
            private bool _disposed = false;

            public KuzuQueryResultHandle(_KuzuQueryResult result)
            {
                _result = result;
            }

            public _KuzuQueryResult Result => _result;

            public bool Read()
            {
                // TODO: 根據 kuzu API 加入讀 row 的邏輯
                return false;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    Native.kuzu_query_result_destroy(ref _result);
                    _disposed = true;
                }
            }
        }
        public KuzuQueryResultHandle ExecuteQuery(string query)
        {
            _KuzuQueryResult result;
            int state;
            state = Native.kuzu_connection_query(ref _conn, query, out result);
            if (state != 0)
                throw new InvalidOperationException("Query failed or returned null result.");

            if (!Native.kuzu_query_result_is_success(ref result))
            {
                string? errorMsg = Marshal.PtrToStringAnsi(Native.kuzu_query_result_get_error_message(ref result));
                Native.kuzu_query_result_destroy(ref result);
                throw new Exception($"Query failed: {errorMsg}");
            }

            return new KuzuQueryResultHandle(result);
        }
        public string Execute(string query)
        {
            try
            {
                var result = ExecuteQuery(query);
                var str_result = GetQueryResultAsString(result.Result);
                result.Dispose();
                return str_result;
            }
            catch (Exception e)
            {
                throw;
            }
        }
        private string GetQueryResultAsString(_KuzuQueryResult result)
        {
            IntPtr strPtr = Native.kuzu_query_result_to_string(ref result);
            return Marshal.PtrToStringAnsi(strPtr) ?? string.Empty;
        }
        public void Dispose()
        {
            Native.kuzu_connection_destroy(ref _conn);
            Native.kuzu_database_destroy(ref _db);
        }
    }
    #region DLL內部結構
    /// <summary>
    /// 對應 C 端的 struct kuzu_database { void* _database; }
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct _KuzuDatabase
    {
        public IntPtr _database;   // C 的 void* _database
    }

    /// <summary>
    /// 對應 C 端的 struct kuzu_connection { void* _connection; }
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct _KuzuConnection
    {
        public IntPtr _connection; // C 的 void* _connection
    }

    /// <summary>
    /// 對應 C 端的 struct kuzu_query_result { void* _query_result; bool _is_owned_by_cpp; }
    /// 在 64 位元下，C 端實際大小 = 8 bytes (_query_result) + 1 byte (bool) 
    /// 然後補 7 bytes padding，所以總長度 16 bytes。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct _KuzuQueryResult
    {
        public IntPtr _query_result;      // C: void* _query_result

        [MarshalAs(UnmanagedType.I1)]
        public bool _is_owned_by_cpp;     // C: bool _is_owned_by_cpp
        // 剩下 .NET 自動補的 7 bytes padding，對齊到 16 bytes。
    }

    /// <summary>
    /// 對應 C 端的 kuzu_system_config struct
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct _KuzuSystemConfig
    {
        public UInt64 buffer_pool_size;      // offset=0
        public UInt64 max_num_threads;       // offset=8

        [MarshalAs(UnmanagedType.I1)]
        public bool enable_compression;      // offset=16

        [MarshalAs(UnmanagedType.I1)]
        public bool read_only;               // offset=17

        // .NET 會自動在 offset=18~23 填 6 bytes padding，讓下一個 UInt64 對齊到 24。

        public UInt64 max_db_size;           // offset=24

        [MarshalAs(UnmanagedType.I1)]
        public bool auto_checkpoint;         // offset=32

        // .NET 會自動在 offset=33~39 填 7 bytes padding。

        public UInt64 checkpoint_threshold;  // offset=40

        // struct 總長度對齊到 48 bytes（因為最後一個 UInt64 在 40∼47）。
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuFlatTuple
    {
        public IntPtr _flat_tuple;

        [MarshalAs(UnmanagedType.I1)]
        public bool _is_owned_by_cpp;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuLogicalType
    {
        public IntPtr _data_type;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuValue
    {
        public IntPtr _value;

        [MarshalAs(UnmanagedType.I1)]
        public bool _is_owned_by_cpp;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuInternalId
    {
        public ulong table_id;
        public ulong offset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuDate
    {
        public int days;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuTimestampNs
    {
        public long value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuTimestampMs
    {
        public long value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuTimestampSec
    {
        public long value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuTimestampTz
    {
        public long value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuTimestamp
    {
        public long value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuInterval
    {
        public int months;
        public int days;
        public long micros;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuQuerySummary
    {
        public IntPtr _query_summary;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KuzuInt128
    {
        public ulong low;
        public long high;
    }

    public enum KuzuDataTypeId : int
    {
        KUZU_ANY = 0,
        KUZU_NODE = 10,
        KUZU_REL = 11,
        KUZU_RECURSIVE_REL = 12,
        KUZU_SERIAL = 13,
        KUZU_BOOL = 22,
        KUZU_INT64 = 23,
        KUZU_INT32 = 24,
        KUZU_INT16 = 25,
        KUZU_INT8 = 26,
        KUZU_UINT64 = 27,
        KUZU_UINT32 = 28,
        KUZU_UINT16 = 29,
        KUZU_UINT8 = 30,
        KUZU_INT128 = 31,
        KUZU_DOUBLE = 32,
        KUZU_FLOAT = 33,
        KUZU_DATE = 34,
        KUZU_TIMESTAMP = 35,
        KUZU_TIMESTAMP_SEC = 36,
        KUZU_TIMESTAMP_MS = 37,
        KUZU_TIMESTAMP_NS = 38,
        KUZU_TIMESTAMP_TZ = 39,
        KUZU_INTERVAL = 40,
        KUZU_DECIMAL = 41,
        KUZU_INTERNAL_ID = 42,
        KUZU_STRING = 50,
        KUZU_BLOB = 51,
        KUZU_LIST = 52,
        KUZU_ARRAY = 53,
        KUZU_STRUCT = 54,
        KUZU_MAP = 55,
        KUZU_UNION = 56,
        KUZU_POINTER = 58,
        KUZU_UUID = 59
    }
    #endregion
    internal static class Native
    {
        #region LIB位置
#if WINDOWS
        const string KuzuLib = @"lib\kuzu\windows\kuzu_shared";
#elif LINUX
        const string KuzuLib = @"lib/kuzu/linux/libkuzu";
#else
        const string KuzuLib = @"lib/kuzu/macos/kuzu";
#endif
        #endregion

        // ---------------------------------------------------
        // (A) kuzu_default_system_config
        //     C 端: KUZU_C_API kuzu_system_config kuzu_default_system_config();
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern _KuzuSystemConfig kuzu_default_system_config();

        // ---------------------------------------------------
        // (B) kuzu_database_init
        //     C 端: KUZU_C_API kuzu_state kuzu_database_init(
        //               const char*          database_path,
        //               kuzu_system_config   system_config,
        //               kuzu_database*       out_database
        //           );
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int kuzu_database_init(
            [MarshalAs(UnmanagedType.LPStr)] string database_path,
            _KuzuSystemConfig system_config,
            out _KuzuDatabase out_database
        );

        // ---------------------------------------------------
        // (C) kuzu_database_destroy
        //     C 端: KUZU_C_API void kuzu_database_destroy(kuzu_database* database);
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kuzu_database_destroy(
            ref _KuzuDatabase database
        );

        // ---------------------------------------------------
        // (D) kuzu_connection_init
        //     C 端: KUZU_C_API kuzu_state kuzu_connection_init(
        //               kuzu_database*   database,
        //               kuzu_connection* out_connection
        //           );
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kuzu_connection_init(
            ref _KuzuDatabase database,
            out _KuzuConnection out_connection
        );

        // ---------------------------------------------------
        // (E) kuzu_connection_destroy
        //     C 端: KUZU_C_API void kuzu_connection_destroy(kuzu_connection* connection);
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kuzu_connection_destroy(
            ref _KuzuConnection connection
        );

        // ---------------------------------------------------
        // (F) kuzu_connection_query
        //     C 端: KUZU_C_API kuzu_state kuzu_connection_query(
        //               kuzu_connection*   connection,
        //               const char*        query,
        //               kuzu_query_result* out_query_result
        //           );
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int kuzu_connection_query(
            ref _KuzuConnection connection,
            [MarshalAs(UnmanagedType.LPStr)] string query,
            out _KuzuQueryResult out_query_result
        );

        // ---------------------------------------------------
        // (G) kuzu_query_result_destroy
        //     C 端: KUZU_C_API void kuzu_query_result_destroy(kuzu_query_result* query_result);
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kuzu_query_result_destroy(
            ref _KuzuQueryResult result
        );

        // ---------------------------------------------------
        // (H) kuzu_query_result_is_success
        //     C 端: KUZU_C_API bool kuzu_query_result_is_success(kuzu_query_result* query_result);
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool kuzu_query_result_is_success(
            ref _KuzuQueryResult result
        );

        // ---------------------------------------------------
        // (I) kuzu_query_result_get_error_message
        //     C 端: KUZU_C_API char* kuzu_query_result_get_error_message(kuzu_query_result* query_result);
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr kuzu_query_result_get_error_message(
            ref _KuzuQueryResult result
        );

        // ---------------------------------------------------
        // (J) kuzu_query_result_to_string
        //     C 端: KUZU_C_API char* kuzu_query_result_to_string(kuzu_query_result* query_result);
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr kuzu_query_result_to_string(
            ref _KuzuQueryResult result
        );

        // ---------------------------------------------------
        // (K) 如果 Native 提供了釋放 C 字符串的函式 (例如 kuzu_destroy_string)，
        //     請務必也宣告它，否則用完 char* 一直累積 C heap memory leak。
        // ---------------------------------------------------
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kuzu_destroy_string(IntPtr str);

        #region 資料庫連線/建立

        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kuzu_database_destroy(IntPtr db);


        #endregion


        #region 查詢相關


        // 4. Get number of columns
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong kuzu_query_result_get_num_columns(ref _KuzuQueryResult result);

        // 5. Get column name
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kuzu_query_result_get_column_name(
            ref _KuzuQueryResult result,
            ulong index,
            out IntPtr out_column_name);

        // 6. Get column data type
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kuzu_query_result_get_column_data_type(
            ref _KuzuQueryResult result,
            ulong index,
            out KuzuLogicalType out_column_data_type);

        // 7. Get number of tuples (rows)
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong kuzu_query_result_get_num_tuples(ref _KuzuQueryResult result);

        // 8. Get query summary 
        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kuzu_query_result_get_query_summary(
            ref _KuzuQueryResult result,
            out KuzuQuerySummary out_summary);

        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool kuzu_query_result_has_next(ref _KuzuQueryResult result);

        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kuzu_query_result_get_next(
    ref _KuzuQueryResult result,
    out KuzuFlatTuple out_flat_tuple);

        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool kuzu_query_result_has_next_query_result(ref _KuzuQueryResult result);

        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kuzu_query_result_get_next_query_result(
    ref _KuzuQueryResult result,
    out _KuzuQueryResult out_next_query_result);

        [DllImport(KuzuLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kuzu_query_result_reset_iterator(ref _KuzuQueryResult result);
        #endregion

    }
}

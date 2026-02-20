using RemoteService;  // 由 svcutil 生成的代理类命名空间
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;  // 仅用于参数化查询中的类型映射，实际远程调用不使用
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Azure.Core.HttpHeader;

namespace OB.Tools
{
    /// <summary>
    /// 仅支持远程数据库操作的工具类，通过 SOAP Web 服务执行 SQL 命令。
    /// 移植自 VB.NET 的 DBTools，保留相同的公共接口但仅实现远程模式。
    /// </summary>
    public class RemoteDBTools
    {
        #region 字段

        private Service1SoapClient _soapClient;
        private bool _isAuthenticated;
        private readonly string _serviceUrl;

        // 登录信息
        private string _userName;
        private string _password;
        private string _database;
        //private string _domain = "Topmix.net";
        private string _lastMessage = "";
        private string _language = System.Globalization.CultureInfo.InstalledUICulture.Name;
        private int _timeOut = 30;

        // 用户信息（延迟加载）
        private UserInfo _userInfo;
        private AccountInfo _accountInfo;

        #endregion

        #region 属性

        public string UserName => _userName;
        public string Password => _password;
        public string Server { get; private set; }  // 对于远程模式，Server 实际是主机名或 IP
        public string Database => _database;
        //public string Domain => _domain;
        public bool LocalLogin => false;  // 始终为 false，表示远程模式
        public bool IsAuthenticated => _isAuthenticated;
        public string LastMessage => _lastMessage;
        public bool Integrate => false;   // 远程模式不使用集成认证
        public string Language
        {
            get => _language;
            set => _language = value;
        }
        public int TimeOut
        {
            get => _timeOut;
            set
            {
                _timeOut = value;
                // 远程模式不需要设置连接超时，但保留属性以兼容接口
            }
        }

        /// <summary>
        /// 加密的用户名密码（用于某些业务场景）
        /// </summary>
        public string UPS => Encrypt($"{UserName}@{Password}", DateTime.Now.ToString("yyyyMMdd"));

        /// <summary>
        /// 用户详细信息（延迟加载）
        /// </summary>
        public UserInfo UserInfo
        {
            get
            {
                if (_userInfo == null)
                    InitializeUserInfoAsync().Wait();  // 同步等待，实际使用建议调用异步方法
                return _userInfo;
            }
        }

        /// <summary>
        /// 账户信息（延迟加载）
        /// </summary>
        public AccountInfo AccountInfo
        {
            get
            {
                if (_accountInfo == null)
                    _accountInfo = new AccountInfo(this);
                return _accountInfo;
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 RemoteDBTools 实例（远程模式）。
        /// </summary>
        /// <param name="serviceUrl">Web 服务地址，例如 "http://server/dataservice/GetData.asmx"</param>
        /// <param name="server">服务器名称或 IP（用于显示，不用于连接）</param>
        /// <param name="database">数据库名称</param>
        /// <param name="userName">用户名</param>
        /// <param name="password">密码</param>
        public RemoteDBTools(string serviceUrl, string server, string database, string userName, string password)
        {
            _serviceUrl = serviceUrl;
            Server = server;
            _database = database;
            _userName = userName;
            _password = password;
        }

        /// <summary>
        /// 简化构造函数，仅接收服务地址（需后续调用 InitializeAsync 完成登录）
        /// </summary>
        public RemoteDBTools(string serviceUrl)
        {
            _serviceUrl = serviceUrl;
        }

        #endregion

        #region 登录与认证

        /// <summary>
        /// 异步登录并初始化远程连接。
        /// </summary>
        public async Task<bool> InitializeAsync(string username, string password, string database)
        {
            _userName = username;
            _password = password;
            _database = database;

            // 从服务地址中提取主机名作为 Server 属性（可选）
            try
            {
                var uri = new Uri(_serviceUrl);
                Server = uri.Host;
            }
            catch { Server = ""; }

            return await WebServiceAuthenticateAsync();
        }

        /// <summary>
        /// 同步登录（为兼容旧代码，内部调用异步并等待）
        /// </summary>
        public bool Authenticate()
        {
            return Task.Run(() => WebServiceAuthenticateAsync()).Result;
        }

        /// <summary>
        /// 远程 Web 服务认证
        /// </summary>
        private async Task<bool> WebServiceAuthenticateAsync()
        {
            var binding = new BasicHttpBinding
            {
                Security = {
                    Mode = BasicHttpSecurityMode.TransportCredentialOnly,
                    Transport = { ClientCredentialType = HttpClientCredentialType.Basic }
                },
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max,
                AllowCookies = true
            };

            var endpoint = new EndpointAddress(_serviceUrl);
            _soapClient = new Service1SoapClient(binding, endpoint);

            // 设置基本认证凭据
            _soapClient.ClientCredentials.UserName.UserName = _userName;
            _soapClient.ClientCredentials.UserName.Password = _password;

            try
            {
                // 1. 指定数据库
                await _soapClient.SetDataBaseAsync(_database);

                // 2. 获取加密的安全字符串
                var comstr = await _soapClient.CommSecurityStringAsync();

                // 3. 解密得到密钥，并用其加密用户名和密码
                var decryptedKey = Decrypt(comstr, "19283746");
                var encryptedCredentials = Encrypt($"{_userName}@{_password}", decryptedKey);

                // 4. 发送加密凭据完成登录
                _isAuthenticated = await _soapClient.SetCommStringAsync(encryptedCredentials);
                return _isAuthenticated;
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                _isAuthenticated = false;
                return false;
            }
        }

        /// <summary>
        /// 初始化用户信息（调用 Web 服务的 GetLoginInfo）
        /// </summary>
        private async Task InitializeUserInfoAsync()
        {
            if (!_isAuthenticated)
                throw new InvalidOperationException("请先登录。");

            try
            {
                var loginInfo = await _soapClient.GetLoginInfoAsync();
                _userInfo = new UserInfo(loginInfo);
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                _userInfo = new UserInfo(); // 空对象
            }
        }

        #endregion

        #region 核心数据库操作方法

        /// <summary>
        /// 执行非查询 SQL 语句（INSERT/UPDATE/DELETE）
        /// </summary>
        public async Task<string> ExecuteNonQueryAsync(string sqlCommand)
        {
            EnsureAuthenticated();
            try
            {
                return await _soapClient.ExecuteNonQueryAsync(sqlCommand);
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                return "";
            }
        }

        /// <summary>
        /// 同步版本 ExecuteNonQuery
        /// </summary>
        public string ExecuteNonQuery(string sqlCommand)
        {
            return Task.Run(() => ExecuteNonQueryAsync(sqlCommand)).Result;
        }

        /// <summary>
        /// 执行查询并返回第一行第一列的值
        /// </summary>
        public async Task<string> ExecuteScalarAsync(string sqlCommand)
        {
            EnsureAuthenticated();
            try
            {
                return await _soapClient.ExecuteScalarAsync(sqlCommand);
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                return "";
            }
        }

        /// <summary>
        /// 同步版本 ExecuteScalar
        /// </summary>
        public string ExecuteScalar(string sqlCommand)
        {
            return Task.Run(() => ExecuteScalarAsync(sqlCommand)).Result;
        }

        /// <summary>
        /// 执行查询并返回 DataSet（包含结构信息）
        /// </summary>
        /// <param name="selectCommand">SELECT 语句</param>
        /// <param name="message">输出消息</param>
        /// <param name="runType">0=带主键信息，1=不带</param>
        public async Task<DataSet> GetSelectResultAsync(string selectCommand, string message = "", int runType = 0)
        {
            EnsureAuthenticated();
            try
            {
                var request = new GetSelectResultRequest
                {
                    strSelectCommand = selectCommand,
                    Message = message ?? "",
                    RunType = runType
                };
                var response = await _soapClient.GetSelectResultAsync(request);
                return ConvertXmlToDataSet(response.GetSelectResultResult);
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 同步版本 GetSelectResult
        /// </summary>
        public DataSet GetSelectResult(string selectCommand,  string message, int runType = 0)
        {
            var result = Task.Run(() => GetSelectResultAsync(selectCommand, message, runType)).Result;
            // 注意：异步方法不返回 message，这里只能返回结果，message 无法更新。
            // 建议直接使用异步版本。
            return result;
        }

        /// <summary>
        /// 简化同步版本（不输出 message）
        /// </summary>
        public DataSet GetSelectResult(string selectCommand)
        {
            string msg = "";
            return GetSelectResult(selectCommand,msg, 0);
        }

        /// <summary>
        /// 更新 DataTable 到数据库（通过 Web 服务的 UpdateDataTable）
        /// </summary>
        /// <param name="dsChangeDataSet">包含更改的 DataSet，第一个表为变更数据</param>
        /// <param name="tableName">目标表名，为空则使用 DataTable 的表名</param>
        /// <returns>操作结果字符串，格式如 "0 错误信息" 或 "1 成功信息"</returns>
        public async Task<string> UpdateDataTableAsync(DataSet dsChangeDataSet, string tableName = "")
        {
            EnsureAuthenticated();
            if (dsChangeDataSet == null || dsChangeDataSet.Tables.Count == 0)
                return "0 数据集为空";

            if (string.IsNullOrEmpty(tableName))
                tableName = dsChangeDataSet.Tables[0].TableName;

            if (string.IsNullOrEmpty(tableName))
                return "0 不能修改没有名称的表的数据";

            try
            {
                // 将 DataSet 转换为 ArrayOfXElement（服务端期望的格式）
                var array = ConvertDataSetToArrayOfXElement(dsChangeDataSet, tableName);
                return await _soapClient.UpdateDataTableAsync(array, tableName);
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                return "0 " + ex.Message;
            }
        }

        /// <summary>
        /// 同步版本 UpdateDataTable
        /// </summary>
        public string UpdateDataTable(DataSet dsChangeDataSet, string tableName = "")
        {
            return Task.Run(() => UpdateDataTableAsync(dsChangeDataSet, tableName)).Result;
        }

        /// <summary>
        /// 检查 SQL 语法（远程模式暂不支持，返回提示）
        /// </summary>
        public string CheckGrammar(string expression)
        {
            return "1 远程连接暂时不能执行数据语法检查!";
        }

        #endregion

        #region 参数化查询支持

        /// <summary>
        /// 参数化查询包装类，用于远程模式下将参数嵌入 SQL（防 SQL 注入仍有限）
        /// </summary>
        public class ParameterizedQuery
        {
            public string SQL { get; set; }
            public List<SqlParameter> Parameters { get; private set; }

            public ParameterizedQuery()
            {
                Parameters = new List<SqlParameter>();
            }

            public ParameterizedQuery(string sql) : this()
            {
                SQL = sql;
            }

            public void AddParameter(string name, object value)
            {
                Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
            }

            public void AddParameter(string name, SqlDbType dbType, object value)
            {
                var param = new SqlParameter(name, dbType)
                {
                    Value = value ?? DBNull.Value
                };
                Parameters.Add(param);
            }

            /// <summary>
            /// 将参数化查询转换为无参数 SQL（用于 Web 服务调用）
            /// </summary>
            public string ToParameterlessSQL()
            {
                if (Parameters == null || Parameters.Count == 0)
                    return SQL;

                string result = SQL;

                // 按参数名长度降序排序，避免部分替换问题（如 @ID 和 @ID2）
                var sortedParams = Parameters.OrderByDescending(p => p.ParameterName.Length).ToList();

                foreach (var param in sortedParams)
                {
                    string paramName = param.ParameterName;
                    string paramValue = GetParameterValueAsSQL(param);
                    result = result.Replace(paramName, paramValue);
                }

                return result;
            }

            private string GetParameterValueAsSQL(SqlParameter param)
            {
                if (param.Value == DBNull.Value || param.Value == null)
                    return "NULL";

                object value = param.Value;

                // 根据数据类型转换为 SQL 字面值
                switch (param.SqlDbType)
                {
                    case SqlDbType.NVarChar:
                    case SqlDbType.VarChar:
                    case SqlDbType.Char:
                    case SqlDbType.NChar:
                    case SqlDbType.Text:
                    case SqlDbType.NText:
                        string strVal = value.ToString().Replace("'", "''");
                        if (param.SqlDbType == SqlDbType.NVarChar || param.SqlDbType == SqlDbType.NChar || param.SqlDbType == SqlDbType.NText)
                            return $"N'{strVal}'";
                        else
                            return $"'{strVal}'";

                    case SqlDbType.DateTime:
                    case SqlDbType.SmallDateTime:
                    case SqlDbType.Date:
                        DateTime date = (DateTime)value;
                        return $"'{date:yyyy-MM-dd HH:mm:ss}'";

                    case SqlDbType.Int:
                    case SqlDbType.SmallInt:
                    case SqlDbType.TinyInt:
                    case SqlDbType.BigInt:
                    case SqlDbType.Decimal:
                    case SqlDbType.Money:
                    case SqlDbType.SmallMoney:
                    case SqlDbType.Float:
                    case SqlDbType.Real:
                        return value.ToString();

                    case SqlDbType.Bit:
                        return (bool)value ? "1" : "0";

                    default:
                        // 其他类型转字符串
                        string defVal = value.ToString().Replace("'", "''");
                        return $"'{defVal}'";
                }
            }

            public SqlParameter[] ToParameterArray() => Parameters.ToArray();
        }

        /// <summary>
        /// 安全的参数构造器（辅助类）
        /// </summary>
        public static class SafeParameterBuilder
        {
            public static SqlParameter CreateStringParameter(string name, string value, int size = -1)
            {
                var param = new SqlParameter(name, SqlDbType.NVarChar);
                if (size > 0) param.Size = size;
                param.Value = string.IsNullOrEmpty(value) ? DBNull.Value : (object)value;
                return param;
            }

            public static SqlParameter CreateIntParameter(string name, int? value)
            {
                var param = new SqlParameter(name, SqlDbType.Int);
                param.Value = value.HasValue ? (object)value.Value : DBNull.Value;
                return param;
            }

            public static SqlParameter CreateDateTimeParameter(string name, DateTime? value)
            {
                var param = new SqlParameter(name, SqlDbType.DateTime);
                param.Value = value.HasValue ? (object)value.Value : DBNull.Value;
                return param;
            }

            public static SqlParameter CreateDecimalParameter(string name, decimal? value)
            {
                var param = new SqlParameter(name, SqlDbType.Decimal);
                param.Value = value.HasValue ? (object)value.Value : DBNull.Value;
                return param;
            }

            public static SqlParameter CreateBooleanParameter(string name, bool? value)
            {
                var param = new SqlParameter(name, SqlDbType.Bit);
                param.Value = value.HasValue ? (object)value.Value : DBNull.Value;
                return param;
            }
        }

        /// <summary>
        /// 执行参数化查询（远程模式自动转换为无参数 SQL）
        /// </summary>
        public async Task<string> ExecuteParameterizedQueryAsync(ParameterizedQuery query)
        {
            string sql = query.ToParameterlessSQL();
            return await ExecuteNonQueryAsync(sql);
        }

        public string ExecuteParameterizedQuery(ParameterizedQuery query)
        {
            return Task.Run(() => ExecuteParameterizedQueryAsync(query)).Result;
        }

        /// <summary>
        /// 执行参数化查询并返回标量值
        /// </summary>
        public async Task<string> ExecuteScalarParameterizedQueryAsync(ParameterizedQuery query)
        {
            string sql = query.ToParameterlessSQL();
            return await ExecuteScalarAsync(sql);
        }

        public string ExecuteScalarParameterizedQuery(ParameterizedQuery query)
        {
            return Task.Run(() => ExecuteScalarParameterizedQueryAsync(query)).Result;
        }

        /// <summary>
        /// 执行参数化查询并返回 DataSet
        /// </summary>
        public async Task<DataSet> GetSelectResultParameterizedQueryAsync(ParameterizedQuery query, string message = "", int runType = 0)
        {
            string sql = query.ToParameterlessSQL();
            return await GetSelectResultAsync(sql, message, runType);
        }

        public DataSet GetSelectResultParameterizedQuery(ParameterizedQuery query, string message, int runType = 0)
        {
            return Task.Run(() => GetSelectResultParameterizedQueryAsync(query, message, runType)).Result;
        }

        /// <summary>
        /// 简化参数化查询方法（直接传 SQL 和参数数组）
        /// </summary>
        public async Task<string> ExecuteParameterizedQueryAsync(string sql, params SqlParameter[] parameters)
        {
            var query = new ParameterizedQuery(sql);
            foreach (var p in parameters)
                query.Parameters.Add(p);
            return await ExecuteParameterizedQueryAsync(query);
        }

        public async Task<string> ExecuteScalarParameterizedQueryAsync(string sql, params SqlParameter[] parameters)
        {
            var query = new ParameterizedQuery(sql);
            foreach (var p in parameters)
                query.Parameters.Add(p);
            return await ExecuteScalarParameterizedQueryAsync(query);
        }

        public async Task<DataSet> GetSelectResultParameterizedQueryAsync(string sql, string message, int runType, params SqlParameter[] parameters)
        {
            var query = new ParameterizedQuery(sql);
            foreach (var p in parameters)
                query.Parameters.Add(p);
            return await GetSelectResultParameterizedQueryAsync(query, message, runType);
        }

        #endregion

        #region 辅助方法

        private void EnsureAuthenticated()
        {
            if (!_isAuthenticated || _soapClient == null)
                throw new InvalidOperationException("请先调用 InitializeAsync 或 Authenticate 完成登录。");
        }

        /// <summary>
        /// 将服务返回的 ArrayOfXElement 转换为 DataSet
        /// </summary>
        private DataSet ConvertXmlToDataSet(ArrayOfXElement arrayOfXElement)
        {
            if (arrayOfXElement?.Nodes == null || arrayOfXElement.Nodes.Count == 0)
                return null;

            var doc = new XDocument(new XElement("Root", arrayOfXElement.Nodes));
            using (var reader = doc.CreateReader())
            {
                var ds = new DataSet();
                ds.ReadXml(reader);
                return ds;
            }
        }

        /// <summary>
        /// 将 DataSet 转换为 ArrayOfXElement（用于 UpdateDataTable）
        /// 注意：此方法需要根据实际服务端期望的格式进行调整。
        /// 这里简单地将 DataTable 写入 XML 片段。
        /// </summary>
        private ArrayOfXElement ConvertDataSetToArrayOfXElement(DataSet ds, string tableName)
        {
            var array = new ArrayOfXElement();
            if (ds.Tables.Contains(tableName))
            {
                var dt = ds.Tables[tableName];
                foreach (DataRow row in dt.Rows)
                {
                    // 将每一行转换为一个 XElement（具体结构需与服务端匹配）
                    var elem = new XElement("row");
                    foreach (DataColumn col in dt.Columns)
                    {
                        elem.Add(new XElement(col.ColumnName, row[col]));
                    }
                    array.Nodes.Add(elem);
                }
            }
            return array;
        }

        #endregion

        #region 加密解密（与原始 VB.NET Common 类保持一致）

        public static string Decrypt(string pToDecrypt, string sKey) => Decrypt(pToDecrypt, sKey, Encoding.UTF8);
        public static string Decrypt(string pToDecrypt, string sKey, Encoding coder)
        {
            try
            {
                using (var des = new DESCryptoServiceProvider())
                {
                    int len = pToDecrypt.Length / 2;
                    byte[] inputByteArray = new byte[len];
                    for (int x = 0; x < len; x++)
                    {
                        int i = Convert.ToInt32(pToDecrypt.Substring(x * 2, 2), 16);
                        inputByteArray[x] = (byte)i;
                    }

                    des.Key = Encoding.ASCII.GetBytes(sKey);
                    des.IV = Encoding.ASCII.GetBytes(sKey);

                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(inputByteArray, 0, inputByteArray.Length);
                            cs.FlushFinalBlock();
                        }
                        return coder.GetString(ms.ToArray());
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        public static string Encrypt(string pToEncrypt, string sKey) => Encrypt(pToEncrypt, sKey, Encoding.UTF8);
        public static string Encrypt(string pToEncrypt, string sKey, Encoding coder)
        {
            try
            {
                using (var des = new DESCryptoServiceProvider())
                {
                    byte[] inputByteArray = coder.GetBytes(pToEncrypt);
                    des.Key = Encoding.ASCII.GetBytes(sKey);
                    des.IV = Encoding.ASCII.GetBytes(sKey);

                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(inputByteArray, 0, inputByteArray.Length);
                            cs.FlushFinalBlock();
                        }

                        byte[] encryptedBytes = ms.ToArray();
                        StringBuilder ret = new StringBuilder();
                        foreach (byte b in encryptedBytes)
                        {
                            ret.AppendFormat("{0:X2}", b);
                        }
                        return ret.ToString();
                    }
                }
            }
            catch
            {
                return "";
            }
        }
        #endregion
    }

    #region 用户信息类（简版，可根据实际需求扩展）

    public class UserInfo
    {
        public string LoginName { get; set; }
        public string FullName { get; set; }
        public string FunctionGroup { get; set; }
        public string Department { get; set; }
        public string JobTitle { get; set; }
        public string Account { get; set; }
        public string Country { get; set; }
        public string WorkShop { get; set; }
        public string Team { get; set; }

        public UserInfo() { }

        public UserInfo(LoginInfo loginInfo)
        {
            LoginName = loginInfo.LoginName;
            FullName = loginInfo.FullName;
            FunctionGroup = loginInfo.FunctionGroup;
            Department = loginInfo.Department;
            JobTitle = loginInfo.JobTitle;
            Account = loginInfo.Account;
            Country = loginInfo.Country;
            WorkShop = loginInfo.WorkShop;
            Team = loginInfo.Team;
        }
    }

    public class AccountInfo
    {
        private RemoteDBTools _tools;
        // 根据需要添加属性
        public AccountInfo(RemoteDBTools tools)
        {
            _tools = tools;
            // 可在此通过 _tools 查询数据库初始化信息
        }
    }
    #endregion
}
using Avalonia.Controls.Shapes;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using RemoteService; // 添加服务引用命名空间
using static Azure.Core.HttpHeader;

namespace OB.Tools
{
    public class DBTools
    {
        private readonly bool _isLocal;
        private string? _connectionString;           // 用于本地模式
        private Service1SoapClient _soapClient;      // 修改为正确的类型

        // 服务地址和域（用于远程模式）
        private string _serviceUrl;
        private string _domain = "Topmix.net";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverOrUrl">本地模式传服务器名，远程模式传 Web 服务基础 URL（如 http://server/DataService/GetData.asmx）</param>
        /// <param name="isLocal">true=本地, false=远程</param>
        public DBTools(string serverOrUrl, bool isLocal)
        {
            _isLocal = isLocal;
            if (isLocal)
            {
                _serverAddress = serverOrUrl;
            }
            else
            {
                _serviceUrl = serverOrUrl;
            }
        }

        // 本地模式字段
        private string _serverAddress;
        private string _database;

        // 远程模式：登录成功后保存认证状态
        private bool _isAuthenticated;

        /// <summary>
        /// 登录验证（本地或远程）
        /// </summary>
        public async Task<bool> InitializeAsync(string username, string password, string database)
        {
            if (_isLocal)
            {
                _database = database;
                _connectionString = $"Data Source={_serverAddress};Initial Catalog={database};User ID={username};Password={password};TrustServerCertificate=True";
                try
                {
                    using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                    await connection.OpenAsync();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                // 远程模式：创建 SOAP 客户端
                var binding = new BasicHttpBinding();
                binding.Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;
                binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

                var endpoint = new EndpointAddress(_serviceUrl);
                _soapClient = new Service1SoapClient(binding, endpoint); // 使用正确的客户端类型

                // 设置凭据
                _soapClient.ClientCredentials.UserName.UserName = username;
                _soapClient.ClientCredentials.UserName.Password = password;

                try
                {
                    // 1. 先调用 SetDataBase 指定数据库
                    await _soapClient.SetDataBaseAsync(database);

                    // 2. 获取安全字符串
                    var comstr = await _soapClient.CommSecurityStringAsync();

                    // 3. 解密并加密
                    var decryptedKey = Decrypt(comstr, "19283746");
                    var encryptedCredentials = Encrypt($"{username}@{password}", decryptedKey);

                    // 4. 调用 SetCommString 进行认证
                    _isAuthenticated = await _soapClient.SetCommStringAsync(encryptedCredentials);

                    return _isAuthenticated;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }
        }

        #region 数据库操作方法

        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters = null)
        {
            if (_isLocal)
            {
                // 本地模式：使用 Dapper
                EnsureLocalConnection();
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                return await connection.QueryAsync<T>(sql, parameters);
            }
            else
            {
                // 远程模式：调用 Web 服务的 GetSelectResult
                EnsureRemoteAuthenticated();

                // 构造请求对象
                var request = new GetSelectResultRequest
                {
                    strSelectCommand = sql,
                    Message = "",
                    RunType = 0
                };

                var response = await _soapClient.GetSelectResultAsync(request);
                // 将 ArrayOfXElement 转换为 IEnumerable<T>
                return ConvertArrayOfXElementToEnumerable<T>(response.GetSelectResultResult);
            }
        }

        public async Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            if (_isLocal)
            {
                EnsureLocalConnection();
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                return await connection.ExecuteAsync(sql, parameters);
            }
            else
            {
                EnsureRemoteAuthenticated();
                var result = await _soapClient.ExecuteNonQueryAsync(sql);
                // 假设服务返回的字符串以 "0 " 开头表示成功
                if (result.StartsWith("0 "))
                    return 0; // 或者解析影响行数（如果需要）
                else
                    throw new Exception(result);
            }
        }

        public async Task<object> ExecuteScalarAsync(string sql, object parameters = null)
        {
            if (_isLocal)
            {
                EnsureLocalConnection();
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                return await connection.ExecuteScalarAsync<object>(sql, parameters);
            }
            else
            {
                EnsureRemoteAuthenticated();
                var result = await _soapClient.ExecuteScalarAsync(sql);
                return result;
            }
        }

        #endregion

        #region 辅助方法

        private void EnsureLocalConnection()
        {
            if (string.IsNullOrEmpty(_connectionString))
                throw new InvalidOperationException("请先调用 InitializeAsync 进行本地登录");
        }

        private void EnsureRemoteAuthenticated()
        {
            if (!_isAuthenticated || _soapClient == null)
                throw new InvalidOperationException("请先调用 InitializeAsync 进行远程登录");
        }

        /// <summary>
        /// 将服务返回的 ArrayOfXElement 转换为 IEnumerable<T>
        /// 需要根据实际的 XML 结构实现映射
        /// </summary>
        private IEnumerable<T> ConvertArrayOfXElementToEnumerable<T>(ArrayOfXElement arrayOfXElement)
        {
            // 示例：假设返回的 XML 是一个 DataSet，我们将其转换为 DataTable 再映射
            if (arrayOfXElement == null || arrayOfXElement.Nodes.Count == 0)
                yield break;

            // 方法1：将多个 XElement 组合成一个 XML 文档，然后加载到 DataSet
            var doc = new XDocument(new XElement("Root", arrayOfXElement.Nodes));
            using (var reader = doc.CreateReader())
            {
                var dataSet = new DataSet();
                dataSet.ReadXml(reader);
                if (dataSet.Tables.Count > 0)
                {
                    // 这里需要将 DataTable 转换为 IEnumerable<T>
                    // 可以使用反射或第三方库（如 FastMember）实现
                    // 简化示例：假设 T 与 DataTable 列名匹配
                    var table = dataSet.Tables[0];
                    foreach (DataRow row in table.Rows)
                    {
                        var item = Activator.CreateInstance<T>();
                        foreach (DataColumn col in table.Columns)
                        {
                            var prop = typeof(T).GetProperty(col.ColumnName);
                            if (prop != null && row[col] != DBNull.Value)
                            {
                                prop.SetValue(item, row[col]);
                            }
                        }
                        yield return item;
                    }
                }
            }
        }

        #endregion

        #region 加密解密方法
        // ... 保持不变，你的加密解密方法没有问题 ...
        public static string Decrypt(string pToDecrypt, string sKey)
        {
            return Decrypt(pToDecrypt, sKey, Encoding.Default);
        }

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

        public static string Encrypt(string pToEncrypt, string sKey)
        {
            return Encrypt(pToEncrypt, sKey, Encoding.Default);
        }

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
            catch (Exception)
            {
                return "";
            }
        }
        #endregion
    }
}
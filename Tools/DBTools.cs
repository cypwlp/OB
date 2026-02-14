using Dapper;
using Microsoft.Data.SqlClient; 
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace OB.Tools
{
    public class DBTools
    {
        private string? connectionString;

        // 驗證登錄，初始化連接字符串
        public bool Initialize(string username,string password)
        {
            connectionString = "Data Source=.;Initial Catalog=OB;User ID=" + username + ";Password=" + password + ";Trust Server Certificate=True";
            try
            {
                using var connection = new SqlConnection(connectionString);
                connection.Open();
                return true; // 登錄成功
            }
            catch
            {
                return false; // 登錄失敗
            }
        }

  
        public DBTools() { }

        // 执行查询，返回结果集
        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters = null)
        {
            using var connection = new SqlConnection(connectionString);
            return await connection.QueryAsync<T>(sql, parameters);
        }

        // 执行查询，返回单条数据或默认值
        public async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object parameters = null)
        {
            using var connection = new SqlConnection(connectionString);
            return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
        }

        // 执行增删改操作，返回受影响行数
        public async Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            using var connection = new SqlConnection(connectionString);
            return await connection.ExecuteAsync(sql, parameters);
        }

        // 如果你需要执行存储过程，可以添加一个方法
        public async Task<IEnumerable<T>> QueryProcAsync<T>(string procName, object parameters = null)
        {
            using var connection = new SqlConnection(connectionString);
            return await connection.QueryAsync<T>(procName, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}
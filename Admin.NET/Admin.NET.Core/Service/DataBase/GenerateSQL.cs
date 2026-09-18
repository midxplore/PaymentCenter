// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

public static class BuildSQL
{
    public static string Build(GenerateSQLInput config)
    {
        if (config.SelectConfigs == null || !config.SelectConfigs.Any())
            throw new ArgumentException("至少需要一个选择配置");

        StringBuilder sql = new StringBuilder();

        // SELECT clause
        sql.Append("SELECT ");
        sql.Append(string.Join(", ", config.SelectConfigs.Select(sc =>
            $"{sc.Table}.{sc.Column}")));
        sql.AppendLine();

        // 确定主表（出现频率最高的表）
        var mainTable = DetermineMainTable(config);
        sql.AppendLine($"FROM {mainTable}");

        // 收集所有涉及的表
        var allTables = CollectAllTables(config);
        var joinedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mainTable };

        // 处理JOIN关系 - 优化后的逻辑
        ProcessJoins(sql, config.JoinConfigs, joinedTables, allTables);

        // 添加剩余未连接的表（使用CROSS JOIN）
        AddRemainingTables(sql, allTables, joinedTables);

        // WHERE子句
        ProcessWhereClause(sql, config.WhereConfigs);

        // ORDER BY子句
        ProcessOrderByClause(sql, config.OrderConfigs);

        return sql.ToString();
    }

    private static string DetermineMainTable(GenerateSQLInput config)
    {
        // 统计所有表出现的频率
        var tableFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // 统计SELECT中的表
        foreach (var sc in config.SelectConfigs)
        {
            if (tableFrequency.ContainsKey(sc.Table))
                tableFrequency[sc.Table]++;
            else
                tableFrequency[sc.Table] = 1;
        }

        // 统计JOIN中的表
        foreach (var jc in config.JoinConfigs)
        {
            if (tableFrequency.ContainsKey(jc.LeftTable))
                tableFrequency[jc.LeftTable]++;
            else
                tableFrequency[jc.LeftTable] = 1;

            if (tableFrequency.ContainsKey(jc.RightTable))
                tableFrequency[jc.RightTable]++;
            else
                tableFrequency[jc.RightTable] = 1;
        }

        // 统计WHERE中的表
        foreach (var wc in config.WhereConfigs)
        {
            if (tableFrequency.ContainsKey(wc.Table))
                tableFrequency[wc.Table]++;
            else
                tableFrequency[wc.Table] = 1;
        }

        // 统计ORDER BY中的表
        foreach (var oc in config.OrderConfigs)
        {
            if (tableFrequency.ContainsKey(oc.Table))
                tableFrequency[oc.Table]++;
            else
                tableFrequency[oc.Table] = 1;
        }

        // 选择出现频率最高的表作为主表
        return tableFrequency.OrderByDescending(kv => kv.Value)
                             .ThenBy(kv => kv.Key)
                             .FirstOrDefault().Key;
    }

    private static HashSet<string> CollectAllTables(GenerateSQLInput config)
    {
        var allTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        config.SelectConfigs?.ForEach(sc => allTables.Add(sc.Table));
        config.JoinConfigs?.ForEach(jc =>
        {
            allTables.Add(jc.LeftTable);
            allTables.Add(jc.RightTable);
        });
        config.WhereConfigs?.ForEach(wc => allTables.Add(wc.Table));
        config.OrderConfigs?.ForEach(oc => allTables.Add(oc.Table));

        return allTables;
    }

    private static void ProcessJoins(StringBuilder sql, List<JoinConfig> joinConfigs,
                             HashSet<string> joinedTables, HashSet<string> allTables)
    {
        if (joinConfigs == null) return;

        // 使用队列处理所有JOIN关系
        var joinQueue = new Queue<JoinConfig>(joinConfigs);
        var processedJoins = new HashSet<JoinConfig>();
        bool progressMade;

        do
        {
            progressMade = false;
            var currentQueue = new Queue<JoinConfig>(joinQueue);
            joinQueue.Clear();

            while (currentQueue.Count > 0)
            {
                var join = currentQueue.Dequeue();

                // 如果右表已经连接，跳过
                if (joinedTables.Contains(join.RightTable))
                {
                    processedJoins.Add(join);
                    continue;
                }

                // 如果左表已连接，可以处理这个JOIN
                if (joinedTables.Contains(join.LeftTable))
                {
                    // 构建ON条件
                    var onConditions = join.TableColumns.Select(tc =>
                        $"{join.LeftTable}.{tc.LeftTableColumn} = " +
                        $"{join.RightTable}.{tc.RightTableColumn}");

                    sql.AppendLine($"{join.JoinType} {join.RightTable} ON {string.Join(" AND ", onConditions)}");
                    joinedTables.Add(join.RightTable);
                    processedJoins.Add(join);
                    progressMade = true;
                }
                else
                {
                    // 左表未连接，放回队列稍后处理
                    joinQueue.Enqueue(join);
                }
            }

            // 如果还有未处理的JOIN但队列未变化，尝试添加缺失的表
            if (!progressMade && joinQueue.Count > 0)
            {
                var nextJoin = joinQueue.Peek();

                // 如果左表不在已连接表中，尝试添加它
                if (!joinedTables.Contains(nextJoin.LeftTable) && allTables.Contains(nextJoin.LeftTable))
                {
                    sql.AppendLine($", {nextJoin.LeftTable}");
                    joinedTables.Add(nextJoin.LeftTable);
                    progressMade = true;
                }
            }
        }
        while (progressMade && joinQueue.Count > 0);

        // 处理剩余无法连接的JOIN
        foreach (var join in joinQueue)
        {
            // 如果右表已经连接，跳过
            if (joinedTables.Contains(join.RightTable)) continue;

            // 尝试添加左表
            if (!joinedTables.Contains(join.LeftTable))
            {
                sql.AppendLine($", {join.LeftTable}");
                joinedTables.Add(join.LeftTable);
            }

            // 构建ON条件
            var onConditions = join.TableColumns.Select(tc =>
                $"{join.LeftTable}.{tc.LeftTableColumn} = " +
                $"{join.RightTable}.{tc.RightTableColumn}");

            sql.AppendLine($"{join.JoinType} {join.RightTable} ON {string.Join(" AND ", onConditions)}");
            joinedTables.Add(join.RightTable);
        }
    }

    private static void AddRemainingTables(StringBuilder sql, HashSet<string> allTables, HashSet<string> joinedTables)
    {
        var remainingTables = allTables.Except(joinedTables, StringComparer.OrdinalIgnoreCase).ToList();
        if (!remainingTables.Any()) return;

        foreach (var table in remainingTables)
        {
            sql.AppendLine($", {table}");
            joinedTables.Add(table);
        }
    }

    private static void ProcessWhereClause(StringBuilder sql, List<WhereConfig> whereConfigs)
    {
        if (whereConfigs == null || !whereConfigs.Any()) return;

        sql.Append("WHERE ");
        foreach (var where in whereConfigs)
        {
            string formattedValue = FormatValue(where.Value, where.DataType, where.Operator);
            sql.Append($"{where.Table}.{where.Column} " + $"{where.Operator} {formattedValue} {where.JoinType} ");
        }

        // 移除最后一个逻辑运算符
        sql.Length -= 4;
        sql.AppendLine();
    }

    private static void ProcessOrderByClause(StringBuilder sql, List<OrderConfig> orderConfigs)
    {
        if (orderConfigs == null || !orderConfigs.Any()) return;

        sql.Append("ORDER BY ");
        sql.Append(string.Join(", ", orderConfigs.OrderBy(oc => oc.Sort).Select(oc => $"{oc.Table}.{oc.Column}")));
    }

    /// <summary>
    /// WHERE 数值格式化
    /// </summary>
    /// <param name="value"></param>
    /// <param name="dataType"></param>
    /// <param name="operatorType"></param>
    /// <returns></returns>
    private static string FormatValue(object value, string dataType, string operatorType)
    {
        if (value == null) return "NULL";

        string valueStr = value.ToString();
        if (string.IsNullOrWhiteSpace(valueStr)) return "NULL";

        // 根据数据类型和操作符格式化值
        if (dataType?.ToLower().Contains("varchar") == true ||
            dataType?.ToLower().Contains("text") == true ||
            dataType?.ToLower().Contains("bool") == true ||
            dataType?.ToLower().Contains("datetime") == true ||
            dataType?.ToLower().Contains("timestamp") == true ||
            value is string)
        {
            // 对于LIKE操作符，添加通配符
            if (operatorType?.ToUpper() == "LIKE")
            {
                return $"'%{valueStr}%'";
            }
            else
            {
                return $"'{valueStr}'";
            }
        }
        else
        {
            // 数字类型直接返回
            return valueStr;
        }
    }
}
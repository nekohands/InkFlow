// 数据库迁移入口骨架：本分支尚未定义任何迁移实体。
// docker compose 的 migrations 服务依赖本进程以退出码 0 结束，
// 因此在无迁移可执行时也必须正常返回。
Console.WriteLine("InkFlow.Migrations: no migrations registered yet, exiting cleanly.");
return 0;

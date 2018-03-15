namespace Marvin.Resources.Model
{
    using System;
    using System.Data.Entity.Migrations;
    using Marvin.Model.Npgsql;
    using System.Data.Entity.Migrations.Model;
    using System.Data.Entity.Migrations.Infrastructure;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            ((IDbMigration)this).AddOperation(new AddSchemaOperation("resources"));
            
            CreateTable(
                "resources.ResourceEntity",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        Name = c.String(),
                        LocalIdentifier = c.String(),
                        GlobalIdentifier = c.String(),
                        ExtensionData = c.String(),
                        Type = c.String(),
                        Created = c.DateTime(nullable: false),
                        Updated = c.DateTime(nullable: false),
                        Deleted = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name)
                .Index(t => t.LocalIdentifier)
                .Index(t => t.GlobalIdentifier);
            
            CreateTable(
                "resources.ResourceRelation",
                c => new
                    {
                        Id = c.Long(nullable: false, identity: true),
                        RelationType = c.Int(nullable: false),
                        RelationName = c.String(),
                        SourceId = c.Long(nullable: false),
                        TargetId = c.Long(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("resources.ResourceEntity", t => t.SourceId, cascadeDelete: true)
                .ForeignKey("resources.ResourceEntity", t => t.TargetId, cascadeDelete: true)
                .Index(t => t.SourceId)
                .Index(t => t.TargetId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("resources.ResourceRelation", "TargetId", "resources.ResourceEntity");
            DropForeignKey("resources.ResourceRelation", "SourceId", "resources.ResourceEntity");
            DropIndex("resources.ResourceRelation", new[] { "TargetId" });
            DropIndex("resources.ResourceRelation", new[] { "SourceId" });
            DropIndex("resources.ResourceEntity", new[] { "GlobalIdentifier" });
            DropIndex("resources.ResourceEntity", new[] { "LocalIdentifier" });
            DropIndex("resources.ResourceEntity", new[] { "Name" });
            DropTable("resources.ResourceRelation");
            DropTable("resources.ResourceEntity");
            ((IDbMigration)this).AddOperation(new RemoveSchemaOperation("resources"));
            
        }
    }
}

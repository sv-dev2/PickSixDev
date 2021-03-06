﻿using Orchard.ContentManagement.MetaData;
using Orchard.Core.Contents.Extensions;
using Orchard.Data.Migration;
using Orchard.Environment.Extensions;

namespace Orchard.jQuery.Ias
{
    [OrchardFeature("Orchard.jQuery.Ias.Blogs")]
    public class BlogsMigrations : DataMigrationImpl {

        public int Create() {
            ContentDefinitionManager.AlterTypeDefinition("Blog",
                cfg => cfg
                    .WithPart("InfiniteAjaxScrollingPart")
                        .WithSetting("InfiniteAjaxScrollingTypePartSettings.UseHistory", "False")
            );

            return 1;
        }
    }
}
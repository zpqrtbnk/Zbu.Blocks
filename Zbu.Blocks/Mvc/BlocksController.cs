﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Zbu.Blocks.Mvc
{
    public class BlocksController : RenderMvcController
    {
        private static bool _registered;
        private static string _structuresPropertyAlias = "structures";
        private static Func<BlocksController, string> _getContext;

        public class GetActionResultEventArgs : EventArgs
        {
            public GetActionResultEventArgs(RenderModel model, RenderingStructure structure, string context)
            {
                Model = model;
                Structure = structure;
                Context = context;

                TraceBlocksInHtml = false;
            }

            public RenderModel Model { get; private set; }
            public RenderingStructure Structure { get; private set; }
            public string Context { get; private set; }

            public bool TraceBlocksInHtml { get; set; }

            public ActionResult Result { get; set; }
        }

        public static event EventHandler<GetActionResultEventArgs> GetActionResult;

        public override ActionResult Index(RenderModel model)
        {
            var content = model.Content;

            // compute the rendering structure
            // get a context if a provider has been configured
            var context = _getContext == null ? null : _getContext(this);
            var rs = RenderingStructure.Compute(context, content,
                x => x.GetPropertyValue<IEnumerable<StructureDataValue>>(_structuresPropertyAlias));
            if (rs == null)
                return base.Index(model);

            // event
            if (GetActionResult != null)
            {
                var args = new GetActionResultEventArgs(model, rs, context);
                GetActionResult(this, args);
                TraceBlocksInHtml = args.TraceBlocksInHtml;
                if (args.Result != null)
                    return args.Result;
            }

            // create a basic BlockModel model - if the view wants a generic
            // BlockModel<TContent> then UmbracoViewPage<> will map using the
            // BlockModelTypeConverter.

            // little point creating a strongly typed model here...
            var m = new BlockModel(model.Content, rs, model.CurrentCulture);
            //var m = CreateModel(model.Content, rs, model.CurrentCulture);

            // should we cache?
            var cachesCookie = umbraco.BusinessLogic.StateHelper.Cookies.Caches["macro"] ?? "cache";
            var cache = cachesCookie != "ignore"
                && rs.Cache != null
                && rs.Cache.GetCacheIf(rs, model.Content, null);

            return cache
                ? Renderer.ViewWithCache(ControllerContext, rs, m, cachesCookie == "refresh")
                : View(rs.Source, m);
        }

        //private static BlockModel<T> CreateModel<T>(T content, RenderingBlock rs, CultureInfo culture)
        //    where T : class, IPublishedContent
        //{
        //    return new BlockModel<T>(content, rs, culture);
        //}

        public static class Settings
        {
            public static string StucturesPropertyAlias
            {
                get { return _structuresPropertyAlias; }
                set
                {
                    EnsureWriteable();
                    if (string.IsNullOrWhiteSpace(value))
                        throw new ArgumentException("Cannot be null nor empty.", "value");
                    _structuresPropertyAlias = value;
                }
            }

            public static Func<BlocksController, string> GetContext
            {
                get { return _getContext; }
                set
                {
                    EnsureWriteable();
                    _getContext = value;
                }
            }

            public static Dictionary<string, Func<RenderingBlock, IPublishedContent, ViewDataDictionary, bool>> CacheIf { get { return CacheProfile.CacheIf; } }
            public static Dictionary<string, Func<RenderingBlock, IPublishedContent, ViewDataDictionary, string>> CacheCustom { get { return CacheProfile.CacheCustom; } }
            public static Dictionary<string, CacheProfile> CacheProfiles { get { return CacheProfile.Profiles; } }
        }

        private static void EnsureWriteable()
        {
            if (_registered)
                throw new InvalidOperationException("Cannot change settings once the controller has been registered.");
        }

        public static void Register()
        {
            FilteredControllerFactoriesResolver.Current.InsertType<BlocksControllerFactory>();
            _registered = true;
        }

        public static void HandleThisRequest()
        {
            BlocksControllerFactory.HandleThisRequest();
        }

        public bool TraceBlocksInHtml { get; private set; }
    }
}
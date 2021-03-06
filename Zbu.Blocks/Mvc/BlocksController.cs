﻿using System;
using System.Collections.Generic;
using System.Web;
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
        private static Action<BlockModel, IDictionary<string, object>> _mergeMeta;

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

            // should we cache?
            var cacheMode = rs.Cache == null
                ? CacheMode.Ignore
                : rs.Cache.GetCacheMode(rs, model.Content, null);

            var text = cacheMode == CacheMode.Ignore
                ? Renderer.ViewText(ControllerContext, rs, model.Content, model.CurrentCulture)
                : Renderer.ViewTextWithCache(ControllerContext, rs, model.Content, model.CurrentCulture, cacheMode == CacheMode.Refresh);

            return Content(text);
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

            public static Action<BlockModel, IDictionary<string, object>> MergeMeta
            {
                get { return _mergeMeta; }
                set
                {
                    EnsureWriteable();
                    _mergeMeta = value;
                }
            }

            public static Dictionary<string, Func<RenderingBlock, IPublishedContent, ViewDataDictionary, CacheMode>> CacheMode { get { return CacheProfile.CacheMode; } }
            public static Dictionary<string, Func<HttpRequestBase, RenderingBlock, IPublishedContent, ViewDataDictionary, string>> CacheCustom { get { return CacheProfile.CacheCustom; } }
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

        //internal ViewResult ViewInternal(string viewName, object model)
        //{
        //    return View(viewName, model);
        //}
    }
}
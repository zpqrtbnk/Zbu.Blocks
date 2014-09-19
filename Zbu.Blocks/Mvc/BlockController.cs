﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Zbu.Blocks.Mvc
{
    public abstract class BlockControllerBase
    {
        #region Controller

        internal HtmlHelper Helper { get; set; }
        internal ControllerContext ControllerContext { get; set; }

        protected internal ViewDataDictionary ViewData { get; set; }
        protected internal RenderingBlock Block { get; internal set; }
        protected internal CultureInfo CurrentCulture { get; internal set; }
        protected internal UmbracoHelper Umbraco { get; internal set; }

        internal abstract void SetContent(IPublishedContent content);

        protected abstract string Render();

        public virtual string RenderText()
        {
            return Render();
        }

        protected string Render(BlockModel blockModel)
        {
            string text;
            BlocksController controller;

            if (Helper != null)
            {
                controller = Helper.ViewContext.Controller as BlocksController;
                text = ViewData == null
                    ? Helper.Partial(Block.Source, blockModel).ToString()
                    : Helper.Partial(Block.Source, blockModel, ViewData).ToString();
            }
            else if (ControllerContext != null)
            {
                controller = ControllerContext.Controller as BlocksController;
                if (controller == null)
                    throw new Exception("Oops.");

                // this basically repeats what Controller.View is doing

                var viewEngineResult = ViewEngines.Engines.FindView(ControllerContext, Block.Source, null);
                if (viewEngineResult == null)
                    throw new Exception("Null ViewEngineResult.");
                var view = viewEngineResult.View;
                if (view == null)
                {
                    // we need to generate an exception containing all the locations we searched
                    // copied over from ViewResult source code
                    var locationsText = new StringBuilder();
                    foreach (var location in viewEngineResult.SearchedLocations)
                    {
                        locationsText.AppendLine();
                        locationsText.Append(location);
                    }
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                        "The view '{0}' or its master was not found or no view engine supports the searched locations. The following locations were searched:{1}", // MvcResources.Common_ViewNotFound
                        Block.Source, locationsText));
                }

                controller.ViewData.Model = blockModel;

                using (var sw = new StringWriter())
                {
                    var ctx = new ViewContext(ControllerContext, view,
                                              controller.ViewData,
                                              controller.TempData,
                                              sw);
                    view.Render(ctx, sw);
                    text = sw.ToString();
                }
            }
            else
                throw new Exception("Oops.");

            var traceBlocksInHtml = controller != null && controller.TraceBlocksInHtml;
            return !traceBlocksInHtml
                ? text
                : string.Format("<!-- block:{0} -->{1}{2}{1}<!-- /block:{0} -->{1}",
                    Block.Source, Environment.NewLine, text);
        }

        #endregion

        #region Controllers

        private static readonly Dictionary<string, Func<BlockControllerBase>> BlockControllers
            = new Dictionary<string, Func<BlockControllerBase>>(StringComparer.InvariantCultureIgnoreCase);

        static BlockControllerBase()
        {
            var noargs = new Type[0];

            foreach (var type in global::Umbraco.Core.TypeFinder.FindClassesOfType<BlockControllerBase>())
                foreach (var blockSource in type.GetCustomAttributes(typeof (BlockControllerAttribute), false)
                    .Cast<BlockControllerAttribute>()
                    .Select(x => x.BlockSource))
                {
                    var ctor = type.GetConstructor(noargs);
                    if (ctor == null)
                        throw new Exception(string.Format("Could not find a proper constructor for type \"{0}\".", type));
                    var exprNew = Expression.New(ctor);
                    var expr = Expression.Lambda<Func<BlockControllerBase>>(exprNew);
                    var func = expr.Compile();
                    BlockControllers[blockSource] = func;
                }
        }

        private class DefaultBlockController : BlockControllerBase
        {
            private IPublishedContent _content;

            internal override void SetContent(IPublishedContent content)
            {
                _content = content;
            }

            protected override string Render()
            {
                // create a block model for the block to render
                // use a basic BlockModel and let the view deal with it
                var blockModel = new BlockModel(_content, Block, CurrentCulture);
                return Render(blockModel);
            }
        }

        internal static BlockControllerBase CreateController(HtmlHelper helper, RenderingBlock block, IPublishedContent content, CultureInfo currentCulture, ViewDataDictionary viewData)
        {
            var c = CreateController(block, content, currentCulture);
            c.Helper = helper;
            c.ViewData = viewData;
            return c;
        }

        internal static BlockControllerBase CreateController(ControllerContext context, RenderingBlock block, IPublishedContent content, CultureInfo currentCulture)
        {
            var c = CreateController(block, content, currentCulture);
            c.ControllerContext = context;
            return c;
        }

        private static BlockControllerBase CreateController(RenderingBlock block, IPublishedContent content, CultureInfo currentCulture)
        {
            Func<BlockControllerBase> func;
            var controller = BlockControllers.TryGetValue(block.Source, out func)
                ? func()
                : new DefaultBlockController();

            controller.Block = block;
            controller.SetContent(content);
            controller.CurrentCulture = currentCulture;
            controller.Umbraco = new UmbracoHelper(UmbracoContext.Current, content);

            return controller;
        }

        #endregion
    }

    public abstract class BlockController<T> : BlockControllerBase
        where T : class, IPublishedContent
    {
        #region Controller

        protected T Content { get; private set; }

        internal override void SetContent(IPublishedContent content)
        {
            var t = content as T;
            if (t == null)
                throw new Exception(string.Format("Invalid content type, got \"{0}\", controller expects \"{1}\".",
                    content.GetType().FullName, typeof (T).FullName));
            Content = t;
        }

        #endregion
    }
}

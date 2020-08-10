using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

namespace I0plus.XdUnityUI.Editor
{
    /// <summary>
    ///     GroupElement class.
    ///     based on Baum2.Editor.GroupElement class.
    /// </summary>
    public class GroupElement : Element
    {
        protected readonly Dictionary<string, object> CanvasGroup;
        protected readonly List<object> ComponentsJson;
        protected readonly Dictionary<string, object> ContentSizeFitterJson;

        protected readonly List<Element> Elements;
        protected readonly string FillColorJson;
        protected readonly Dictionary<string, object> LayoutJson;
        protected readonly Dictionary<string, object> MaskJson;
        protected readonly Dictionary<string, object> ScrollRectJson;
        protected Dictionary<string, object> AddComponentJson;
        protected bool? RectMask2D;

        public GroupElement(Dictionary<string, object> json, Element parent, bool resetStretch = false) : base(json,
            parent)
        {
            Elements = new List<Element>();
            var jsonElements = json.Get<List<object>>("elements");
            if (jsonElements != null)
                foreach (var jsonElement in jsonElements)
                {
                    var elem = ElementFactory.Generate(jsonElement as Dictionary<string, object>, this);
                    if (elem != null)
                        Elements.Add(elem);
                }

            Elements.Reverse();
            CanvasGroup = json.GetDic("canvas_group");
            LayoutJson = json.GetDic("layout");
            ContentSizeFitterJson = json.GetDic("content_size_fitter");
            MaskJson = json.GetDic("mask");
            RectMask2D = json.GetBool("rect_mask_2d");
            ScrollRectJson = json.GetDic("scroll_rect");
            FillColorJson = json.Get("fill_color");
            AddComponentJson = json.GetDic("add_component");
            ComponentsJson = json.Get<List<object>>("components");
        }

        public List<Tuple<GameObject, Element>> RenderedChildren { get; private set; }

        public override void Render(RenderContext renderContext, [CanBeNull] ref GameObject targetObject,
            GameObject parentObject)
        {
            GetOrCreateSelfObject(renderContext, ref targetObject, parentObject);

            RenderedChildren = RenderChildren(renderContext, targetObject);
            ElementUtil.SetupCanvasGroup(targetObject, CanvasGroup);
            ElementUtil.SetupChildImageComponent(targetObject, RenderedChildren);
            ElementUtil.SetupFillColor(targetObject, FillColorJson);
            ElementUtil.SetupContentSizeFitter(targetObject, ContentSizeFitterJson);
            ElementUtil.SetupLayoutGroup(targetObject, LayoutJson);
            ElementUtil.SetupLayoutElement(targetObject, LayoutElementJson);
            ElementUtil.SetupComponents(targetObject, ComponentsJson);
            ElementUtil.SetupMask(targetObject, MaskJson);
            ElementUtil.SetupRectMask2D(targetObject, RectMask2D);
            // ScrollRectを設定した時点で、はみでたContentがアジャストされる　PivotがViewport内に入っていればOK
            GameObject goContent = null;
            if (RenderedChildren.Count > 0) goContent = RenderedChildren[0].Item1;
            ElementUtil.SetupScrollRect(targetObject, goContent, ScrollRectJson);
            ElementUtil.SetupRectTransform(targetObject, RectTransformJson);
        }

        public override void RenderPass2(List<Tuple<GameObject, Element>> selfAndSiblings)
        {
            var self = selfAndSiblings.Find(tuple => tuple.Item2 == this);
            var scrollRect = self.Item1.GetComponent<ScrollRect>();
            if (scrollRect)
            {
                // scrollRectをもっているなら、ScrollBarを探してみる
                var scrollbars = selfAndSiblings
                    .Where(goElem => goElem.Item2 is ScrollbarElement) // 兄弟の中からScrollbarを探す
                    .Select(goElem => goElem.Item1.GetComponent<Scrollbar>()) // ScrollbarコンポーネントをSelect
                    .ToList();
                scrollbars.ForEach(scrollbar =>
                {
                    switch (scrollbar.direction)
                    {
                        case Scrollbar.Direction.LeftToRight:
                            scrollRect.horizontalScrollbar = scrollbar;
                            break;
                        case Scrollbar.Direction.RightToLeft:
                            scrollRect.horizontalScrollbar = scrollbar;
                            break;
                        case Scrollbar.Direction.BottomToTop:
                            scrollRect.verticalScrollbar = scrollbar;
                            break;
                        case Scrollbar.Direction.TopToBottom:
                            scrollRect.verticalScrollbar = scrollbar;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                });
            }
        }

        protected List<Tuple<GameObject, Element>> RenderChildren(RenderContext renderContext, GameObject parent,
            Action<GameObject, Element> callback = null)
        {
            var list = new List<Tuple<GameObject, Element>>();
            foreach (var element in Elements)
            {
                GameObject go = null;
                element.Render(renderContext, ref go, parent);
                if (go.transform.parent != parent.transform) Debug.Log("No parent set" + go.name);

                //if (element.IsPrefab)
                //{
                //    //TODO: Check if prefab names are truly unique or if the components in Adobe XD can have the same name
                //    if(!renderContext.Prefabs.ContainsKey(go.name))
                //        renderContext.Prefabs.Add(go.name,go);
                //    else
                //    {
                //        var oldGo = go;
                //        go = (GameObject)PrefabUtility.InstantiatePrefab(renderContext.Prefabs[oldGo.name],oldGo.transform.parent);

                //        GameObject.DestroyImmediate(oldGo);
                //    }
                //}

                list.Add(new Tuple<GameObject, Element>(go, element));
                if (callback != null) callback.Invoke(go, element);
            }

            foreach (var element in Elements) element.RenderPass2(list);

            RenderedChildren = list;
            return list;
        }
    }
}
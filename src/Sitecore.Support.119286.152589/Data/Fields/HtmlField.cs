﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using HtmlAgilityPack;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Links;
using Sitecore.Resources.Media;
using Sitecore.SecurityModel;

namespace Sitecore.Support.Data.Fields
{
  public class HtmlField : Sitecore.Data.Fields.HtmlField
  {
    private static MethodInfo _addLink = typeof(Sitecore.Data.Fields.HtmlField).GetMethod("AddLink", BindingFlags.NonPublic | BindingFlags.Static);

    private static MethodInfo _getLinkedToItem = typeof(Sitecore.Data.Fields.HtmlField).GetMethod("GetLinkedToItem", BindingFlags.Instance | BindingFlags.NonPublic);

    public HtmlField(Field innerField) : base(innerField)
    {
    }
    
    public override void ValidateLinks(LinksValidationResult result)
    {
      Assert.ArgumentNotNull(result, "result");
      string value = base.Value;
      if (!string.IsNullOrEmpty(value))
      {
        HtmlDocument htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(value);
        AddTextLinks(result, htmlDocument);
        AddMediaLinks(result, htmlDocument);
      }
    }

  private void AddMediaLinks(LinksValidationResult result, HtmlDocument document)
    {
      Assert.ArgumentNotNull(result, "result");
      Assert.ArgumentNotNull(document, "document");
      HtmlNodeCollection htmlNodeCollection = document.DocumentNode.SelectNodes("//img");
      if (htmlNodeCollection != null)
      {
        foreach (HtmlNode item in (IEnumerable<HtmlNode>)htmlNodeCollection)
        {
          AddMediaLink(result, item);
        }
      }
    }
    
    private void AddTextLinks(LinksValidationResult result, HtmlDocument document)
    {
      Assert.ArgumentNotNull(result, "result");
      Assert.ArgumentNotNull(document, "document");
      HtmlNodeCollection htmlNodeCollection = document.DocumentNode.SelectNodes("//a[@href]");
      if (htmlNodeCollection != null)
      {
        foreach (HtmlNode item in (IEnumerable<HtmlNode>)htmlNodeCollection)
        {
          AddTextLink(result, item);
        }
      }
    }

    private void AddMediaLink(LinksValidationResult result, HtmlNode node)
    {
      Assert.ArgumentNotNull(result, "result");
      Assert.ArgumentNotNull(node, "node");
      string src = node.GetAttributeValue("src", string.Empty);
      if (!string.IsNullOrEmpty(src) && !IsExternalLink(src) && !MediaManager.Config.MediaPrefixes.All((string prefix) => src.IndexOf(prefix, StringComparison.InvariantCulture) == -1))
      {
        try
        {
          DynamicLink dynamicLink = DynamicLink.Parse(src);
          Item item = base.InnerField.Database.GetItem(dynamicLink.ItemId);
          _addLink.Invoke(this, new object[] { result, item, src });
        }
        catch
        {
          _addLink.Invoke(this, new object[] { result, null, src });
        }
      }
    }
    
    private void AddTextLink(LinksValidationResult result, HtmlNode node)
    {
      Assert.ArgumentNotNull(result, "result");
      Assert.ArgumentNotNull(node, "node");
      string href = node.GetAttributeValue("href", string.Empty);
      if (!string.IsNullOrEmpty(href) && !IsExternalLink(href))
      {
        List<string> list = new List<string>();
        list.Add("~/link.aspx?");
        list.AddRange(MediaManager.Config.MediaPrefixes);
        if (!list.All((string prefix) => href.IndexOf(prefix, StringComparison.InvariantCulture) == -1))
        {
          try
          {
            Item linkedToItem = _getLinkedToItem.Invoke(this, new object[] { href }) as Item;
            _addLink.Invoke(this, new object[] { result, linkedToItem, href });
          }
          catch
          {
            _addLink.Invoke(this, new object[] { result, null, href });
          }
        }
      }
    }

    private bool IsExternalLink(string link)
    {
      Assert.ArgumentNotNull((object)link, "link");
      Uri uri = null;
      if (HttpContext.Current != null)
      {
        uri = HttpContext.Current.Request.Url;
      }
      if (uri == null && !string.IsNullOrEmpty(Globals.ServerUrl))
      {
        uri = new Uri(Globals.ServerUrl);
      }

      // 119286: check prefices to avoid false broken links reproting.
      List<string> list = new List<string>();
      list.Add("~/link.aspx?");
      list.AddRange(MediaManager.Config.MediaPrefixes);
      if (uri != null)
      {
        if (link.StartsWith($"{uri.Scheme}://{uri.Host}/", StringComparison.InvariantCultureIgnoreCase) || list.Any((string prefix) => link.StartsWith(prefix)))
        {
          return false;
        }

        return true;
      }
      return true;
    }
  }
}
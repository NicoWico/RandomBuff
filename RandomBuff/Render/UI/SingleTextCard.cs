﻿using RandomBuff.Core.Buff;
using RandomBuff.Core.SaveData;
using RandomBuff.Render.CardRender;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomBuff.Render.UI
{
    internal class SingleTextCard
    {
        FSprite _ftexture;
        public FContainer Container { get; private set; }
        public RenderTexture RenderTexture { get => _cardRenderer.cardCameraController.targetTexture; }

        public FSprite CardTexture
        {
            get
            {
                return _ftexture;
            }
        }

        public Vector3 Rotation
        {
            get => new Vector3(_cardRenderer.Rotation.x, _cardRenderer.Rotation.y, _ftexture.rotation);
            set
            {
                _cardRenderer.Rotation = value;
                _ftexture.rotation = value.z;
            }
        }
        public Vector2 Position
        {
            get => _ftexture.GetPosition();
            set => _ftexture.SetPosition(value);
        }
        public float Scale
        {
            get => _ftexture.scale;
            set => _ftexture.scale = value;
        }
        public float Alpha
        {
            get => _ftexture.alpha;
            set => _ftexture.alpha = value;
        }

        public bool Highlight
        {
            get => _cardRenderer.EdgeHighlight;
            set => _cardRenderer.EdgeHighlight = value;
        }

        public bool Grey
        {
            get => _cardRenderer.Grey;
            set => _cardRenderer.Grey = value;
        }

        public string Text
        {
            get => _cardRenderer.textController.Text;
            set => _cardRenderer.textController.Text = value;
        }

        //卡牌效果控制
        internal SingleTextCardRenderer _cardRenderer;
   
        public SingleTextCard(string text)
        {
            int id = CardRendererManager.NextLegalID;
            Container = new FContainer();

            _cardRenderer = CardRendererManager.GetSingleTextRenderer(text);
            _ftexture = _cardRenderer.CleanGetTexture();
            Container.AddChild(_ftexture);
            Text = text;
        }

        public void Update()
        {
        }

        public void GrafUpdate(float timeStacker)
        {
        }

        public void Destroy()
        {
            CardRendererManager.RecycleCardRenderer(_cardRenderer);
            Container.RemoveAllChildren();
            Container.RemoveFromContainer();
            _ftexture.RemoveFromContainer();
        }
    }
}

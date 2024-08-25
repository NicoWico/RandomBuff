using RWCustom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;
using Newtonsoft.Json.Linq;

namespace RandomBuff.Render.CardRender
{
    internal class CardTextController : MonoBehaviour
    {
        static float fadeInOutModeLength = 1f;
        static float changeScrollDirModeLength = 4f;
        static float switchToAutoWaitLength = 5;

        public TMP_Text textMesh;
        BuffCardRenderer _renderer;

        internal GameObject _textObjectA;

        bool _firstInit;
        bool _needInit;
        bool _isTitle;

        Color _opaqueColor;
        Color _transparentColor;

        bool _textNeedRefresh;

        bool _textNeedUpdate;
        string _text;
        string Text
        {
            get => _text;
            set
            {
                if(value != _text)
                {
                    _textNeedUpdate = true;
                    _text = value;
                }
            }
        }
        float textMeshLength;

        float _modeTimer;
        Mode currentMode = Mode.Scroll;
        float _maxScrollVel = 1f;
        float _scrolledLength;
        float _minScrolledLength;
        float _maxScrolledLength;
        bool _scrollReverse;

        float _commitedScroll;
        float _scrollVel;


        float _alpha;
        float _targetAlpha = 0f;
        public bool Fade
        {
            get => _targetAlpha == 0f;
            set
            {
                if(value != Fade)
                {
                    _targetAlpha = value ? 0f : 1f;
                    if (_isTitle)
                        _renderer.cardHighlightFrontController.DarkGradient = !value;
                }
            }
        }

        TMP_CharacterInfo[] origCharInfos;
        //Vector3[] origVertices;
        Vector3[][] meshInfoVertices;


        public void Init(BuffCardRenderer renderer, Transform parent, TMP_FontAsset font, Color color, string text, bool isTitle, InGameTranslator.LanguageID id)
        {
            _renderer = renderer;
            _opaqueColor = color;
            _isTitle = isTitle;
            _transparentColor = new Color(color.r, color.g, color.b, 0f);

            if (!_firstInit)
            {
                _textObjectA = new GameObject($"BuffCardTextObjectA");

                _textObjectA.layer = 8;
                textMesh = SetupTextMesh(_textObjectA, font, isTitle);
                _firstInit = true;
            }

            textMesh.text = _text = " ";//刷新之前的文本

            if (isTitle)
            {
                textMesh.fontSize = id == InGameTranslator.LanguageID.Chinese ? 7 : 6;
                var rectTransform = _textObjectA.GetComponent<RectTransform>();
                rectTransform.pivot = new Vector2(0.5f, 0f);
                textMesh.alignment = TextAlignmentOptions.Center;
                rectTransform.sizeDelta = new Vector2(3f, 1f);
                rectTransform.localPosition = new Vector3(0f, -0.5f, -0.01f);
                textMesh.enableWordWrapping = false;
                textMesh.margin = new Vector4(0.1f, 0f, 0f, 0f);
                //textMesh.outlineWidth = 5f;
                //textMesh.outlineColor = Color.black;
                textMesh.alpha = 0f;
                _maxScrollVel = 1f;

                (textMesh as TextMeshPro).m_renderer.material.EnableKeyword("UNDERLAY_ON");
                (textMesh as TextMeshPro).m_renderer.material.SetColor(TMPro.ShaderUtilities.ID_UnderlayColor, Color.black);
                (textMesh as TextMeshPro).m_renderer.material.SetFloat(TMPro.ShaderUtilities.ID_UnderlayDilate, 1f);
                (textMesh as TextMeshPro).m_renderer.material.SetFloat(TMPro.ShaderUtilities.ID_UnderlaySoftness, 0.7f);
            }
            else
            {
                textMesh.fontSize = 3;
                var rectTransform = _textObjectA.GetComponent<RectTransform>();
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                textMesh.alignment = TextAlignmentOptions.TopLeft;
                rectTransform.sizeDelta = new Vector2(2.8f, 4.8f);
                rectTransform.localPosition = new Vector3(0f, 0f, -0.01f);
                textMesh.SetOutlineThickness(0f);
                _maxScrollVel = 0.25f;
                textMesh.outlineColor = Color.white;

                (textMesh as TextMeshPro).m_renderer.material.EnableKeyword("UNDERLAY_ON");
                (textMesh as TextMeshPro).m_renderer.material.SetColor(TMPro.ShaderUtilities.ID_UnderlayColor, Color.black);
                (textMesh as TextMeshPro).m_renderer.material.SetFloat(TMPro.ShaderUtilities.ID_UnderlayDilate, 1f);
                (textMesh as TextMeshPro).m_renderer.material.SetFloat(TMPro.ShaderUtilities.ID_UnderlaySoftness, 0.7f);
            }

            Text = text;
            _textNeedUpdate = true;
            _needInit = false;
            textMesh.alpha = 0f;


            SwitchMode(Mode.Scroll);

            TMP_Text SetupTextMesh(GameObject obj, TMP_FontAsset font, bool isTitle)
            {
                var rectTransform = obj.AddComponent<RectTransform>();
                rectTransform.SetParent(parent);

                obj.AddComponent<MeshRenderer>();
                var textMesh = obj.AddComponent<TextMeshPro>();

                textMesh.font = font;
                //textMesh.enableCulling = true;
                obj.transform.localEulerAngles = Vector3.zero;
               
                return textMesh;
            }
        }

        void Update()
        {
            if (_needInit)
                return;

            if (_alpha == 0 && _targetAlpha == 0)
                return;

            if (_textNeedUpdate)
            {
                textMesh.font.HasCharacters(_text, out var missing, true, true);

                if(missing != null)
                {
                    string missed = "";
                    foreach (var character in missing)
                    {
                        missed += (char)character;
                    }
                    BuffPlugin.LogWarning($"Loading text : {_text}, missing characters : {missed}");
                }

                textMesh.text = _text;
                _textNeedUpdate = false;
                _textNeedRefresh = true;
                return;
            }

            if (_textNeedRefresh)
            {
                RefreshTextInfo();
                UpdateTextMesh(true);
            }


            if (textMesh == null || meshInfoVertices == null || _textNeedRefresh)
                return;

            if (_alpha != _targetAlpha)
            {
                if (_alpha > _targetAlpha)
                    _alpha -= Time.deltaTime * 0.5f;
                else if (_alpha < _targetAlpha)
                    _alpha += Time.deltaTime * 0.5f;

                if ((_targetAlpha == 1f && _alpha > _targetAlpha) || (_targetAlpha == 0f && _alpha < _targetAlpha))
                    _alpha = _targetAlpha;
                UpdateTextMesh(_alpha == _targetAlpha);
            }

            if (_isTitle)
            {
                if (_renderer.normal.z < 0)
                    return;
            }
            else
            {
                if (_renderer.normal.z >= 0)
                    return;
            }

            if (_alpha == 0f)
                return;

            if (currentMode == Mode.Scroll)
            {
                if (!_scrollReverse)
                {
                    if (_scrolledLength <= _maxScrolledLength)
                    {
                        float lerped = Mathf.Lerp(_scrolledLength, _scrolledLength + _maxScrollVel, 0.05f) - _scrolledLength;
                        float maxStep = _maxScrollVel * Time.deltaTime;
                        if (lerped > maxStep)
                            lerped = maxStep;

                        _scrolledLength += lerped;
                        if (_scrolledLength >= _maxScrolledLength)
                        {
                            _scrolledLength = _maxScrolledLength;
                            SwitchMode(Mode.ChangeScrollDir);
                        }
                        UpdateTextMesh();
                    }
                }
                else
                {
                    if (_scrolledLength >= _minScrolledLength)
                    {
                        float lerped = Mathf.Lerp(_scrolledLength, _scrolledLength - _maxScrollVel, 0.05f) - _scrolledLength;
                        float maxStep = -_maxScrollVel * Time.deltaTime;
                        if (lerped < maxStep)
                            lerped = maxStep;

                        _scrolledLength += lerped;
                        if (_scrolledLength <= _minScrolledLength)
                        {
                            _scrolledLength = _minScrolledLength;
                            SwitchMode(Mode.ChangeScrollDir);
                        }
                        UpdateTextMesh();
                    }
                }
            }
            else if (currentMode == Mode.ChangeScrollDir)
            {
                if (_modeTimer < changeScrollDirModeLength)
                    _modeTimer += Time.deltaTime;
                else
                {
                    _scrollReverse = !_scrollReverse;
                    SwitchMode(Mode.Scroll);
                }
            }
            else if(currentMode == Mode.Manually)
            {
                if(_modeTimer < switchToAutoWaitLength)
                {
                    _modeTimer += Time.deltaTime;

                    _scrollVel = Mathf.Lerp(_scrollVel, _commitedScroll * 60f, Time.deltaTime);
                    _scrolledLength += _scrollVel * Time.deltaTime;

                    if (_scrolledLength < _minScrolledLength)
                        _scrolledLength = _minScrolledLength;
                    else if(_scrolledLength > _maxScrolledLength)
                        _scrolledLength = _maxScrolledLength;
                    UpdateTextMesh();
                }
                else
                {
                    _commitedScroll = 0f;
                    _scrollVel = 0f;
                    SwitchMode(Mode.Scroll);
                }
            }
        }

        internal void SwitchMode(Mode newMode)
        {
            if (newMode == currentMode)
                return;
            currentMode = newMode;
            _modeTimer = 0f;
            _scrollVel = 0f;
            _commitedScroll = 0f;
        }

        /// <summary>
        /// 根据参数t更新文本的状态，t在0到1之间
        /// </summary>
        /// <param name="t"></param>
        void UpdateTextMesh(bool forceUpdate = false)
        {
            if (_isTitle)
            {
                if (_renderer.normal.z < 0 && !forceUpdate)
                    return;

                for (int i = 0; i < textMesh.textInfo.characterCount; i++)
                {
                    var charInfo = textMesh.textInfo.characterInfo[i];
                    if (!charInfo.isVisible)
                        continue;

                    var verts = textMesh.textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
                    var colors = textMesh.textInfo.meshInfo[charInfo.materialReferenceIndex].colors32;
                    for (int v = 0; v < 4; v++)
                    {
                        var orig = meshInfoVertices[charInfo.materialReferenceIndex][charInfo.vertexIndex + v];
                        verts[charInfo.vertexIndex + v] = orig + new Vector3(-_scrolledLength, 0f, 0f);

                        colors[charInfo.vertexIndex + v] = Color.Lerp(_transparentColor, _opaqueColor, _alpha * (1.4f - Mathf.Abs(verts[charInfo.vertexIndex + v].x)) / 0.2f);
                    }
                }
            }
            else
            {
                if (_renderer.normal.z >= 0 && !forceUpdate)
                    return;

                for (int i = 0; i < textMesh.textInfo.characterCount; i++)
                {
                    var charInfo = textMesh.textInfo.characterInfo[i];
                    if (!charInfo.isVisible)
                        continue;

                    var verts = textMesh.textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;
                    var colors = textMesh.textInfo.meshInfo[charInfo.materialReferenceIndex].colors32;
                    for (int v = 0; v < 4; v++)
                    {
                        var orig = meshInfoVertices[charInfo.materialReferenceIndex][charInfo.vertexIndex + v];
                        verts[charInfo.vertexIndex + v] = orig + new Vector3(0f, _scrolledLength, 0f);

                        colors[charInfo.vertexIndex + v] = Color.Lerp(_transparentColor, _opaqueColor, _alpha * (2.45f - Mathf.Abs(verts[charInfo.vertexIndex + v].y)) / 0.2f);
                    }
                }
            }

            for (int i = 0; i < textMesh.textInfo.meshInfo.Length; i++)
            {
                textMesh.textInfo.meshInfo[i].mesh.vertices = textMesh.textInfo.meshInfo[i].vertices;
                textMesh.textInfo.meshInfo[i].mesh.colors32 = textMesh.textInfo.meshInfo[i].colors32;
            }


            for (int i = 0; i < textMesh.textInfo.meshInfo.Length; i++)
            {
                textMesh.UpdateGeometry(textMesh.textInfo.meshInfo[i].mesh, i);
            }

            _renderer.cardCameraController.CardDirty = true;
        }

        void RefreshTextInfo()
        {
            meshInfoVertices = new Vector3[textMesh.textInfo.meshInfo.Length][];
            for(int i = 0; i < textMesh.textInfo.meshInfo.Length; i++)
            {
                var vert = textMesh.textInfo.meshInfo[i].vertices;
                meshInfoVertices[i] = new Vector3[vert.Length];

                Array.Copy(vert, meshInfoVertices[i], vert.Length);
            }

            if (_isTitle)
            {
                float xLeft = float.MaxValue;
                float xRight = float.MinValue;

                foreach (var chara in textMesh.textInfo.characterInfo)
                {
                    if (!chara.isVisible)
                        continue;

                    var origVertices = meshInfoVertices[chara.materialReferenceIndex];
                    for (int v = 0; v < 4 && chara.vertexIndex + v < origVertices.Length; v++)
                    {
                        //BuffPlugin.Log($"vertex : {chara.vertexIndex + v} , chara : {chara.vertexIndex}");
                        var vertex = origVertices[chara.vertexIndex + v];
                        if (vertex.x < xLeft)
                            xLeft = vertex.x;
                        if (vertex.x > xRight)
                            xRight = vertex.x;
                    }
                }
                textMeshLength = xRight - xLeft;
                _maxScrolledLength = Mathf.Max(xRight - 1.4f, 0f);
                _minScrolledLength = Mathf.Min(xLeft + 1.4f, 0f);
                _scrolledLength = _minScrolledLength;
            }
            else
            {
                float yDown = float.MaxValue;
                float yUp = float.MinValue;

                foreach (var chara in textMesh.textInfo.characterInfo)
                {
                    if (!chara.isVisible)
                        continue;
                    var origVertices = meshInfoVertices[chara.materialReferenceIndex];
                    for (int v = 0; v < 4 && chara.vertexIndex + v < origVertices.Length; v++)
                    {
                        //BuffPlugin.Log($"vertex : {chara.vertexIndex + v} , chara : {chara.vertexIndex}");
                        var vertex = origVertices[chara.vertexIndex + v];
                        if (vertex.y < yDown)
                            yDown = vertex.y;
                        if (vertex.y > yUp)
                            yUp = vertex.y;
                    }
                }
                textMeshLength = yUp - yDown;
                _maxScrolledLength = Mathf.Max(textMeshLength - 4.8f, 0f);
                _minScrolledLength = 0f;
                _scrolledLength = _minScrolledLength;

            }
            _textNeedRefresh = false;
        }

        void OnDisable()
        {
            _needInit = true;
            _alpha = 0f;
            _targetAlpha = 0f;
            meshInfoVertices = null;
        }

        public void CommitScroll(float scroll)
        {
            if(scroll != 0f)
            {
                SwitchMode(Mode.Manually);
                _modeTimer = 0f;
            }
            _commitedScroll = -scroll;//反过来才对，不知道为什么
        }
        internal enum Mode
        {
            Wait,
            Scroll,
            ChangeScrollDir,
            Manually
        }
    }
}

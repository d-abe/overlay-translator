"""
OCR（画像からテキスト抽出）機能
Google Cloud Vision API、Groq Vision API、またはEasyOCRを使用
"""
import os
import io
import base64
from PIL import Image
from dotenv import load_dotenv
from app.logger import debug, info, warning, error, exception

class OCRProcessor:
    def __init__(self):
        # 環境変数を再読み込み
        load_dotenv(override=True)
        
        # OCR方法を選択（環境変数で指定可能、デフォルトはgoogle）
        self.method = os.getenv('OCR_METHOD', 'google').lower()
        self.vision_client = None
        self.easyocr_reader = None
        self.groq_client = None
        
        # 使用する方法を初期化
        if self.method == 'google':
            self._init_google_vision()
        elif self.method == 'groq':
            self._init_groq_vision()
        elif self.method == 'easyocr':
            self._init_easyocr()
        else:
            # デフォルトでGoogle Visionを試し、失敗したらEasyOCRにフォールバック
            try:
                self._init_google_vision()
                self.method = 'google'
            except:
                self._init_easyocr()
                self.method = 'easyocr'
    
    def _init_google_vision(self):
        """Google Cloud Vision APIを使用するOCRを初期化"""
        try:
            from google.cloud import vision
            from google.api_core import client_options as client_options_lib
            
            # 認証方法を確認
            # 1. 環境変数GOOGLE_APPLICATION_CREDENTIALSでサービスアカウントJSONファイルのパスを指定
            # 2. 環境変数GOOGLE_API_KEYでAPIキーを指定
            credentials_path = os.getenv('GOOGLE_APPLICATION_CREDENTIALS')
            api_key = os.getenv('GOOGLE_API_KEY')
            
            if api_key:
                # APIキーを使用する場合、ClientOptionsでAPIキーを指定
                client_options = client_options_lib.ClientOptions(api_key=api_key)
                self.vision_client = vision.ImageAnnotatorClient(client_options=client_options)
            elif credentials_path and os.path.exists(credentials_path):
                # サービスアカウントJSONファイルを使用
                os.environ['GOOGLE_APPLICATION_CREDENTIALS'] = credentials_path
                self.vision_client = vision.ImageAnnotatorClient()
            else:
                # デフォルトの認証情報を試す（gcloud認証情報など）
                try:
                    self.vision_client = vision.ImageAnnotatorClient()
                except Exception as e:
                    raise ValueError(
                        "Google Cloud Vision APIの認証情報が見つかりません。\n"
                        "GOOGLE_API_KEYまたはGOOGLE_APPLICATION_CREDENTIALSを設定してください。"
                    )
                
        except ImportError:
            raise ImportError(
                "Google Cloud Vision APIがインストールされていません。\n"
                "pip install google-cloud-vision でインストールしてください。"
            )
        except Exception as e:
            error(f"Google Vision APIの初期化に失敗しました: {e}")
            raise
    
    def _init_groq_vision(self):
        """Groq Vision APIを使用するOCRを初期化"""
        try:
            from openai import OpenAI
            
            api_key = os.getenv('GROQ_API_KEY')
            if not api_key:
                raise ValueError(
                    "GROQ_API_KEYが環境変数に設定されていません。\n"
                    "設定画面からAPIキーを設定してください。"
                )
            
            # Groq APIを使用（OpenAI互換API）
            self.groq_client = OpenAI(
                api_key=api_key,
                base_url="https://api.groq.com/openai/v1"
            )
            
        except ImportError:
            raise ImportError(
                "OpenAIライブラリがインストールされていません。\n"
                "pip install openai でインストールしてください。"
            )
        except Exception as e:
            error(f"Groq Vision APIの初期化に失敗しました: {e}")
            raise
    
    def _init_easyocr(self):
        """EasyOCRを使用するOCRを初期化"""
        try:
            import easyocr
            # 英語と日本語をサポート
            self.easyocr_reader = easyocr.Reader(['en', 'ja'], gpu=False)
        except ImportError:
            raise ImportError(
                "EasyOCRがインストールされていません。\n"
                "pip install easyocr でインストールしてください。"
            )
        except Exception as e:
            error(f"EasyOCRの初期化に失敗しました: {e}")
            raise
    
    def extract_text(self, image):
        """画像からテキストを抽出"""
        if self.method == 'google':
            return self._extract_with_google_vision(image)
        elif self.method == 'groq':
            return self._extract_with_groq_vision(image)
        elif self.method == 'easyocr' and self.easyocr_reader:
            return self._extract_with_easyocr(image)
        else:
            # フォールバック: まずGoogle Visionを試し、失敗したらEasyOCR
            try:
                if not self.vision_client:
                    self._init_google_vision()
                return self._extract_with_google_vision(image)
            except:
                try:
                    if not self.easyocr_reader:
                        self._init_easyocr()
                    return self._extract_with_easyocr(image)
                except Exception as e:
                    error(f"OCRエラー: {e}")
                    return ""
    
    def _extract_with_google_vision(self, image):
        """Google Cloud Vision APIを使用してテキストを抽出"""
        try:
            # vision_clientが初期化されていない場合は初期化
            if not self.vision_client:
                self._init_google_vision()
            
            # PIL Imageをbytesに変換
            buffered = io.BytesIO()
            image.save(buffered, format="PNG")
            image_content = buffered.getvalue()
            
            # google-cloud-visionライブラリを使用
            from google.cloud import vision
            vision_image = vision.Image(content=image_content)
            response = self.vision_client.text_detection(image=vision_image)
            texts = response.text_annotations
            
            if texts:
                # 最初の要素が全テキストを含む
                extracted_text = texts[0].description.strip()
                info(f"[OCR結果 - Google Vision] {extracted_text}")
                return extracted_text
            warning("[OCR結果 - Google Vision] テキストが検出されませんでした")
            return ""
            
        except Exception as e:
            # Google Vision APIエラーの場合、EasyOCRにフォールバック
            warning(f"Google Vision APIエラー（EasyOCRにフォールバック）: {e}")
            try:
                if not self.easyocr_reader:
                    self._init_easyocr()
                return self._extract_with_easyocr(image)
            except:
                return ""
    
    def _extract_with_groq_vision(self, image):
        """Groq Vision APIを使用してテキストを抽出"""
        try:
            # groq_clientが初期化されていない場合は初期化
            if not self.groq_client:
                self._init_groq_vision()
            
            # PIL Imageをbytesに変換してbase64エンコード
            buffered = io.BytesIO()
            # 画像サイズを最適化（Groq Vision APIの制限: base64で4MB、33メガピクセル）
            # 画像が大きすぎる場合はリサイズ
            max_size = (2048, 2048)  # 約4メガピクセル
            if image.size[0] * image.size[1] > max_size[0] * max_size[1]:
                image = image.copy()
                image.thumbnail(max_size, Image.Resampling.LANCZOS)
            
            image.save(buffered, format="PNG")
            image_content = buffered.getvalue()
            
            # base64エンコード
            base64_image = base64.b64encode(image_content).decode('utf-8')
            
            # Groq Vision APIを使用（llama-4-scoutまたはllama-4-maverick）
            # デフォルトはllama-4-scout（高速）
            vision_model = os.getenv('GROQ_VISION_MODEL', 'meta-llama/llama-4-scout-17b-16e-instruct')
            
            response = self.groq_client.chat.completions.create(
                model=vision_model,
                messages=[
                    {
                        "role": "user",
                        "content": [
                            {
                                "type": "text",
                                "text": "この画像に含まれるすべてのテキストを正確に抽出してください。画像内の文字をそのまま、改行やスペースも含めて正確に出力してください。"
                            },
                            {
                                "type": "image_url",
                                "image_url": {
                                    "url": f"data:image/png;base64,{base64_image}"
                                }
                            }
                        ]
                    }
                ],
                temperature=0.1,  # 低い温度でより正確なOCR
                max_tokens=2048
            )
            
            extracted_text = response.choices[0].message.content.strip()
            info(f"[OCR結果 - Groq Vision] {extracted_text}")
            return extracted_text
            
        except Exception as e:
            # Groq Vision APIエラーの場合、EasyOCRにフォールバック
            warning(f"Groq Vision APIエラー（EasyOCRにフォールバック）: {e}")
            try:
                if not self.easyocr_reader:
                    self._init_easyocr()
                return self._extract_with_easyocr(image)
            except:
                return ""
    
    def _extract_with_easyocr(self, image):
        """EasyOCRを使用してテキストを抽出"""
        try:
            # PIL Imageをnumpy配列に変換
            import numpy as np
            img_array = np.array(image)
            
            # EasyOCRでテキスト抽出
            results = self.easyocr_reader.readtext(img_array)
            
            # 結果をテキストに結合
            texts = []
            for (bbox, text, confidence) in results:
                if confidence > 0.5:  # 信頼度が50%以上のもののみ
                    texts.append(text)
            
            text = '\n'.join(texts)
            extracted_text = text.strip()
            info(f"[OCR結果 - EasyOCR] {extracted_text}")
            return extracted_text
            
        except Exception as e:
            error(f"EasyOCRエラー: {e}")
            return ""


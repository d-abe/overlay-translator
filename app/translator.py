"""
AI翻訳機能（Groq API使用）
"""
import os
from openai import OpenAI
from dotenv import load_dotenv
from app.logger import error

class Translator:
    def __init__(self):
        # 環境変数を再読み込み
        load_dotenv(override=True)
        
        api_key = os.getenv('GROQ_API_KEY')
        if not api_key:
            raise ValueError("GROQ_API_KEYが環境変数に設定されていません。設定画面からAPIキーを設定してください。")
        
        # Groq APIを使用（OpenAI互換API）
        self.client = OpenAI(
            api_key=api_key,
            base_url="https://api.groq.com/openai/v1"
        )
    
    def translate_to_japanese(self, text):
        """テキストを日本語に翻訳"""
        try:
            # Groqで利用可能なモデル（最新のモデルリスト: https://console.groq.com/docs/models）
            # デフォルト: llama-3.1-8b-instant（高速・低コスト）
            model = os.getenv('GROQ_MODEL', 'llama-3.1-8b-instant')
            
            response = self.client.chat.completions.create(
                model=model,
                messages=[
                    {
                        "role": "system",
                        "content": "あなたは優秀な翻訳者です。入力されたテキストを自然な日本語に翻訳してください。"
                    },
                    {
                        "role": "user",
                        "content": f"以下のテキストを日本語に翻訳してください:\n\n{text}"
                    }
                ],
                temperature=0.3,
                max_tokens=1000
            )
            
            translated_text = response.choices[0].message.content.strip()
            return translated_text
            
        except Exception as e:
            error(f"翻訳エラー: {e}")
            return f"翻訳エラー: {str(e)}"


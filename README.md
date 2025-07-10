# NashiLLM

介紹
---
使用本地電腦資源去架設大型語言模型的服務，用於避免傳送敏感資料給服務提供者。

測試環境
---
硬體:
>CPU: Intel Core i5 13600k
>
>GPU: Nvidia GTX1080Ti
>
>RAM: 32G DDR5

作業系統:
> Windows 11 專業版 24H2

軟體:
>Nvidia Cuda Toolkit 12.9
>
>cuDNN 9.10.1
>
LLM模型:
>Microsoft Phi-4-mini (unsloth/Phi-4-mini-instruct-GGUF/Phi-4-mini-instruct-Q4_K_M.gguf)
>
Embedded模型:
>BAAI/bge-m3 
>

使用方法
---
系統建置
> 安裝Nvidia Cuda Toolkit 12.9
> 
> 安裝cuDNN 9.10.1
> 
> 將cuDNN加入系統環境參數當中 (bin底下含有dll的資料夾, 例:C:\Program Files\NVIDIA\CUDNN\v9.10\bin\11.8)
> 

LLM模型
> 下載LLM模型放置於路徑(.\\models\\[modelname]\\)
> 根據電腦硬體規格調整設定餐數
> 
Embedded模型
> 下載Embedded模型model.onnx, tokenizer.json 以及 model.onnx_data放置於(.\\embedded_model\\) 
>   
設定檔:
> 移動Setting底下的所有檔案至執行目錄中並且按照自己所使用的模型進行設定
> 
因還在開發階段, 目前主要是透過console去進行互動/測試


## ✅ 已完成功能

- [x] 儲存對話資料(DuckDB)
- [x] 支援多使用者同時對模型進行詢問 (需測試)
- [x] 支援上下文對話
- [x] 記錄對話大意用於回應使用者 (需調整)
- [x] 建立多個模型實例用於負載平衡
- [X] RAG功能 (本地資料與用戶個人資料)
---

## ⛏️ 開發/測試中
- [ ] Kuzu Wrapper
- [ ] KAG功能 (本地資料與用戶個人資料)
      
---

## 📝 TODO
- [ ] 單檔RAG搜尋 (OpenAI方法?)
- [ ] 註冊/登入系統 
- [ ] WebAPI

---

## 🪛 已知問題

---

## 🧾 License

本專案採用 [GNU GPLv3](LICENSE) 授權。

如需其他授權（例如不公開原始碼的商業授權），請聯絡著作權人：  
- Email：kenny8379@gmail.com  
- GitHub Issues：<https://github.com/NashiKanjou/ArsCore/issues>

---

### 第三方套件與商標

本專案可能使用下列第三方元件，且保留其原始授權及商標聲明：  
- Microsoft .NET Framework  
- NVIDIA CUDA Toolkit  
- DuckDB (MIT License)  
- KùzuDB (MIT License)  

各商標為其所有者之財產，與本專案無關。

> 本專案由個人開發，使用 Visual Studio Community Edition。  
> 僅用於個人開發與開源目的，與任何任職單位無關。

>This project is developed and maintained by an individual using **Visual Studio Community Edition**,  
>exclusively for personal, open-source, and non-commercial use.  
>It is **not affiliated with nor developed on behalf of any employer or organization**.

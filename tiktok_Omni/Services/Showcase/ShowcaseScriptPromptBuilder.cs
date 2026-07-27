using System;
using tiktok_Omni.Services;

namespace tiktok_Omni.Services.Showcase

{

    /// <summary>Prompt Gemini vision cho Showcase — xem ảnh sản phẩm, tự suy chủ đề, sắp cảnh, viết voiceover + prompt clip.</summary>

    public static class ShowcaseScriptPromptBuilder

    {

        public static string Build(string productName, string userTheme, string userProductType, string userClipModeId, string userOutputAspectId, int sceneCount, AppSettings settings = null)

        {

            var framework = sceneCount >= 5
                ? "AIDA (Attention – Interest – Desire – Action, cảnh cuối lồng CTA)"
                : sceneCount >= 4
                    ? "PAS (Pain – Agitate – Solve – CTA)"
                    : "Showcase " + sceneCount + " cảnh — mạch ngắn: mở thu hút, giữa nhấn sản phẩm, kết CTA";

            var roleList = sceneCount >= 5
                ? "attention, interest, desire, action"
                : sceneCount >= 4
                    ? "pain, agitate, solve, cta"
                    : "attention, interest, desire, action, pain, agitate, solve, cta — gán 1 role/cảnh (cảnh cuối ưu tiên action hoặc cta)";

            var themeInstruction = string.IsNullOrWhiteSpace(userTheme)

                ? "Chủ đề quảng cáo KHÔNG được người dùng cung cấp — hãy TỰ suy luận góc QC phù hợp nhất từ ảnh."

                : "Chủ đề quảng cáo do người dùng chỉ định: \"" + userTheme.Trim() + "\". Hãy tôn trọng đúng chủ đề này khi viết kịch bản.";

            var productTypeInstruction = string.IsNullOrWhiteSpace(userProductType)

                ? "Loại sản phẩm/trang phẩm: TỰ suy từ ảnh nếu người dùng chưa chọn."

                : "Loại sản phẩm do người dùng chỉ định: \"" + userProductType.Trim() + "\". Dùng đúng từ vựng chi tiết cho loại này trong voiceover (tiếng Việt) và prompt clip (tiếng Anh).";

            var clipModeId = ShowcaseClipModePresets.ResolveIdForGemini(userClipModeId);

            var clipModeInstruction = BuildClipModeInstruction(clipModeId);

            var outputAspect = ShowcaseOutputAspectPresets.Resolve(userOutputAspectId, null);
            var aspectInstruction = outputAspect.GeminiHint;

            var zoomOnlyExtra = string.Equals(clipModeId, ShowcaseClipModePresets.ZoomOnlyId, StringComparison.Ordinal)

                ? "\r\nCHẾ ĐỘ CHỈ ZOOM: Bỏ qua quy tắc flatlay→veo. MỌI cảnh (flatlay lẫn on_model) đều clip_tool=zoom; veo_prompt=\"\"; kling_prompt=\"\"; zoom_style + zoom_hint bắt buộc.\r\n"

                : string.Empty;

            var safeProduct = string.IsNullOrWhiteSpace(productName) ? "(không rõ tên, hãy tự đặt tên gợi nhớ)" : productName.Trim();
            var sfxList = ShowcaseSfxCatalog.BuildGeminiIdList(settings ?? new AppSettings());
            var musicList = OmniAudioLibrary.BuildGeminiMusicIdList(settings ?? new AppSettings());



            return

                "Bạn là giám đốc sáng tạo video quảng cáo TikTok ngắn, thể loại \"Showcase sản phẩm\" — mỗi ảnh sản phẩm là 1 phân cảnh, mục tiêu khiến khách hình dung đủ khía cạnh sản phẩm và muốn mua ngay dù chưa có nhu cầu.\r\n" +

                "Sản phẩm: " + safeProduct + ".\r\n" +

                productTypeInstruction + "\r\n" +

                clipModeInstruction + zoomOnlyExtra + "\r\n" +

                aspectInstruction + "\r\n" +

                "Bạn được cung cấp " + sceneCount + " ảnh sản phẩm THEO THỨ TỰ ĐÍNH KÈM (ảnh số 1 đến " + sceneCount + "). Hãy XEM KỸ nội dung, góc chụp, chi tiết từng ảnh trước khi viết.\r\n" +

                themeInstruction + "\r\n\r\n" +

                "YÊU CẦU BẮT BUỘC:\r\n" +

                "1) Tự SẮP XẾP LẠI thứ tự " + sceneCount + " ảnh theo mạch quảng cáo thuyết phục nhất (KHÔNG cần giữ thứ tự gốc) — áp dụng khung " + framework + ".\r\n" +

                "2) Viết 1 câu HOOK mở đầu (tối đa 12 từ tiếng Việt, giật gân, giữ chân người xem trong 3 giây đầu) và 1 câu CTA kết thúc (mềm mại, có lời kêu gọi hành động rõ ràng, tối đa 18 từ).\r\n" +

                "3) Với MỖI cảnh, viết: 'scene_title' (2-4 từ), 'voiceover' (tiếng Việt, 5-8 giây, KHÔNG lặp ý), 'clip_duration_seconds' (số giây clip gợi ý 3–12, căn độ dài đọc voiceover; cảnh silent ~4–5), 'image_kind' (flatlay hoặc on_model), 'clip_tool' (veo/zoom/kling), và prompt phù hợp công cụ.\r\n" +

                "4) BẮT BUỘC giữ NHẤT QUÁN lighting, color palette và camera style xuyên suốt toàn bộ prompt clip.\r\n" +

                "5) 'image_index' PHẢI tham chiếu đúng số thứ tự ảnh gốc (1.." + sceneCount + ") — mỗi ảnh dùng đúng 1 lần.\r\n" +

                "6) 'role' chỉ nhận đúng 1 trong các giá trị: " + roleList + ".\r\n" +
                "7) HIỆU ỨNG ÂM THANH (SFX) — KHÔNG phải cảnh nào cũng cần:\r\n" +
                "   - hook_text là lớp riêng; CẢNH 1 trên storyboard KHÔNG đồng nghĩa hook thoại (cảnh đầu có thể silent).\r\n" +
                "   - Root 'hook_sfx_id' (+ hint): tối đa 1 tiếng MỞ video (impact/whoosh ~0s) — hoặc \"none\".\r\n" +
                "   - Mỗi cảnh 'sfx_id': mặc định \"none\"; CHỈ chọn sfx khi chuyển cảnh/reveal thật sự cần (thường 0–2 cảnh giữa, không phải tất cả).\r\n" +
                "   - 'cta_sfx_id' riêng cho CTA (cảnh cuối), không trùng sfx cảnh cuối nếu đã có.\r\n" +
                "   Id CHỈ từ: " + sfxList + ". 'sfx_placement' = scene_start | scene_middle | scene_end.\r\n" +
                "8) NHẠC NỀN: chọn ĐÚNG 1 track cho cả video — 'background_music_id' CHỈ từ danh sách: "
                + musicList + " (hoặc \"none\" nếu không phù hợp); 'background_music_hint' 5-12 từ tiếng Việt (mood, tempo, vì sao hợp sản phẩm/chủ đề). Nhạc loop phía sau thoại — ưu tiên không lấn át giọng đọc.\r\n\r\n" +

                "PHÂN LOẠI ẢNH & QUY TẮC PROMPT:\r\n" +

                "A) image_kind = flatlay (ảnh không có người):\r\n" +

                "   - clip_tool = veo.\r\n" +

                "   - veo_prompt: bắt đầu \"FLATLAY product shot —\"; mỗi cảnh kỹ thuật KHÁC NHAU (macro dolly, tay không mặt, props, parallax…).\r\n" +

                "   - kling_prompt và zoom_hint để trống.\r\n" +

                "B) image_kind = on_model (ảnh có người mặc sản phẩm):\r\n" +

                "   - clip_tool theo chế độ người dùng (xem mục CHẾ ĐỘ CLIP).\r\n" +

                "   - clip_tool = veo → veo_prompt Flow-safe (chỉ vải/camera/bối cảnh, KHÔNG animate người).\r\n" +

                "   - clip_tool = zoom → BẮT BUỘC zoom_style (CHỈ một trong: " + ShowcaseZoomStyleCatalog.BuildGeminiStyleList() + ") — app FFmpeg render đúng kiểu; zoom_hint ngắn (tiếng Anh, 5-12 từ, mô tả chi tiết trong ảnh, vd \"slow push-in on mandarin collar\").\r\n" +

                "   - clip_tool = kling → kling_prompt tiếng Anh I2V: CHỈ chuyển động KHỚP ảnh (cùng bối cảnh/pose), KHÔNG bịa location mới.\r\n" +

                "C) veo_prompt on-model Flow-safe — bắt đầu \"ON-MODEL fashion lookbook —\"; CẤM: wearer, face, hand adjusts, walking, celebrity, person…\r\n" +

                "E) kling_prompt (CHỈ khi clip_tool=kling — Kling I2V, KHÁC veo_prompt):\r\n" +
                "   - KHÔNG dùng \"ON-MODEL fashion lookbook —\", \"FLATLAY\", hay copy nguyên veo_prompt.\r\n" +
                "   - QUY TẮC VÀNG: CHỈ mô tả chi tiết TRANG PHỤC & BỐI CẢNH NHÌN THẤY TRONG ẢNH — KHÔNG bịa thêu/hoa văn/nút/cổ tay nếu ảnh trơn.\r\n" +
                "   - CẤM: beautiful, young, gorgeous, sexy, celebrity, supermodel — và KHÔNG mô tả địa điểm/hành động KHÔNG có trong ảnh.\r\n" +
                "   - CHUYỂN ĐỘNG VỪA PHẢI: video phải CÓ cảm giác động; tránh gió mạnh, đi xa, xoay, runway.\r\n" +
                "   - CHUYỂN ĐỘNG KHỚP POSE trong ảnh:\r\n" +
                "       • tay chạm tóc/cầm tà/pose phức tạp → subtle weight shift hoặc hand lowers naturally (KHÔNG one slow step mặc định)\r\n" +
                "       • đứng thẳng, chân rõ, nền trống → one slow step forward được phép\r\n" +
                "   - CÔNG THỨC = trang phục (theo ảnh) + same setting as the photo + COMBO 2-3 lớp NHẸ: cơ thể + vải + very slow cinematic push-in.\r\n" +
                "   - BẮT BUỘC: \"face and outfit stay consistent\".\r\n" +
                "F) Áo dài trong kling_prompt (XEM ẢNH trước khi viết):\r\n" +
                "   - Có thêu/hoa văn RÕ trong ảnh → nói \"embroidery\" / \"floral pattern\"; ảnh lụa TRƠN → \"smooth plain white silk panels\", CẤM nói embroidery.\r\n" +
                "   - Preserve: \"preserve ao dai silhouette, mandarin collar and visible fabric details from the photo\" (KHÔNG ghi embroidery nếu ảnh không có).\r\n" +
                "   - Mẫu áo TRƠN: \"White silk ao dai on model in the same bright columned hallway as the photo. Subtle weight shift forward, dress panels and hem sway gently in soft breeze, very slow cinematic push-in. Preserve ao dai silhouette, mandarin collar and smooth plain silk fabric. Face and outfit stay consistent. smooth cinematic motion, no text overlay\"\r\n" +
                "   - Mẫu áo CÓ THÊU: \"Red silk ao dai with gold embroidery on collar on model in the same garden setting as the photo. One slow step forward, embroidered panels sway gently in soft breeze, very slow push-in on collar embroidery. Preserve ao dai shape and visible embroidery from the photo. Face and outfit stay consistent. smooth cinematic motion, no text overlay\"\r\n" +

                "D) Mỗi veo_prompt/kling_prompt: 1-2 câu tiếng Anh; kết thúc \"smooth cinematic motion, no text overlay\" (KHÔNG ghi thời lượng giây — người dùng chọn 5s/7s/10s trên Veo/Kling).\r\n\r\n" +

                "Trả về DUY NHẤT JSON object thuần túy (không markdown), đúng khung:\r\n" +

                "{\"theme\":\"...\",\"hook_text\":\"...\",\"cta_text\":\"...\",\"background_music_id\":\"none\",\"background_music_hint\":\"\",\"hook_sfx_id\":\"none\",\"hook_sfx_hint\":\"\",\"cta_sfx_id\":\"none\",\"cta_sfx_hint\":\"\",\"scenes\":[{\"order\":1,\"role\":\"...\",\"scene_title\":\"...\",\"voiceover\":\"...\",\"clip_duration_seconds\":6.5,\"image_kind\":\"flatlay\",\"clip_tool\":\"veo\",\"veo_prompt\":\"...\",\"kling_prompt\":\"\",\"zoom_style\":\"\",\"zoom_hint\":\"\",\"image_index\":1,\"sfx_id\":\"none\",\"sfx_hint\":\"\",\"sfx_placement\":\"scene_start\"}]}";

        }



        private static string BuildClipModeInstruction(string clipModeId)

        {

            switch (clipModeId)

            {

                case "veo_kling":

                    return "CHẾ ĐỘ CLIP (bắt buộc): Tiết kiệm chất lượng cao — flatlay → clip_tool=veo; on_model → clip_tool=kling (KHÔNG dùng zoom).";

                case "zoom_kling":

                    return "CHẾ ĐỘ CLIP (bắt buộc): Không Veo — flatlay → clip_tool=zoom (zoom_style " + ShowcaseZoomStyleCatalog.BuildGeminiStyleList() + " + zoom_hint); on_model → clip_tool=kling.";

                case "veo_only":

                    return "CHẾ ĐỘ CLIP (bắt buộc): CHỈ VEO — mọi cảnh clip_tool=veo; on_model dùng veo_prompt Flow-safe; KHÔNG zoom, KHÔNG kling.";

                case "kling_only":

                    return "CHẾ ĐỘ CLIP (bắt buộc): CHỈ KLING — mọi cảnh clip_tool=kling; kling_prompt bắt buộc; veo_prompt và zoom để trống.";

                case "zoom_only":

                    return "CHẾ ĐỘ CLIP (bắt buộc): CHỈ ZOOM trong app — mọi cảnh clip_tool=zoom; KHÔNG veo, KHÔNG kling; zoom_style (" + ShowcaseZoomStyleCatalog.BuildGeminiStyleList() + ") + zoom_hint 5-12 từ tiếng Anh.";

                case "custom":
                case "gemini_suggest":

                    return "CHẾ ĐỘ CLIP (Gemini gợi ý): TỰ chọn clip_tool từng cảnh — flatlay thường veo; on_model chọn veo (Flow-safe), zoom (Ken Burns + zoom_style + zoom_hint) hoặc kling (I2V) tùy ảnh và mạch QC.";

                case "veo_zoom":

                default:

                    return "CHẾ ĐỘ CLIP (bắt buộc): Tiết kiệm — flatlay → clip_tool=veo; on_model → clip_tool=zoom (KHÔNG dùng kling). Khi zoom: zoom_style (" + ShowcaseZoomStyleCatalog.BuildGeminiStyleList() + ") + zoom_hint.";

            }

        }

    }

}



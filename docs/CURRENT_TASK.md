# الحالة الحالية وخطوات الاستمرار

آخر تحديث: 2026-10-24، بتوقيت الرياض. هذه الوثيقة هي المرجع الموحد للحالة والتسليم؛ دُمجت فيها تفاصيل وثيقة التسليم المؤرخة لتجنب تكرار المعلومات وتناقض حالتها.

## الطلب الحالي: دعم أغلب المواقع المعروفة وتوسيع توافق استخراج وتنزيل الوسائط — 2026-10-24

- طلب المستخدم: «خلي التطبيق يدعم اغلب المواقع المعروفه»، مع الالتزام بعدم إعادة كوميتات `javhd` الملغاة وعدم تثبيت أي برامج على جهاز المستخدم.
- ما تم فحصه وتنفيذه لتحقيق التوافق الشامل مع أشهر المنصات (YouTube، TikTok، Instagram، X/Twitter، Facebook، Reddit، Twitch، Kick، SoundCloud، Vimeo، Dailymotion، Bilibili، Pinterest، Snapchat، Threads، VK، Streamable، وغيرها):
  1. **دعم الصيغ المدمجة (Video+Audio Muxed Formats) والصوتيات في `Media.cs` و`Format.cs` و`YtdlpExecutableService.cs`**:
     - أغلب المواقع خارج YouTube (مثل Instagram وX/Twitter وTikTok وFacebook وReddit وTwitch Clips وVimeo وPinterest وKick) توفّر بثًا مدمجًا واحدًا يحوي الفيديو والصوت معًا (`hasVideoFormats == true` و`hasAudioFormats == false`)، وكان `Media.cs` في هذه الحالة يضيف `BestVideo`/`WorstVideo` فقط دون `BestAudio`/`WorstAudio`، مما يجعل قائمة صيغ الصوت فارغة (لا تحوي سوى `None`) أو يؤدي لتنزيل فيديو صامت عند اختيار صيغة فيديو منفصلة أو فشل استخراج الصوت (`bestaudio`) لعدم وجود مسار صوتي مستقل.
     - حُدّث `Nickvision.Parabolic.Shared/Models/Media.cs` ليضمن إدراج `Format.BestAudio` و`Format.WorstAudio` دائمًا في `Formats` متى وُجدت صيغ فيديو، وحُدّث `Nickvision.Parabolic.Shared/Services/YtdlpExecutableService.cs` ليضيف `/best` (`/worst`) تلقائيًا إلى محدد الصوت (`bestaudio/best`) عند التحويل إلى ملف صوتي (`FileType.IsAudio`) حتى يستخرج الصوت بنجاح من المواقع ذات البث المدمج.
     - حُدّث `Nickvision.Parabolic.Shared/Models/Format.cs` للتعرف على الصيغ الصوتية عندما تكون `vcodec == "none"` حتى لو غاب نص `resolution == "audio only"` (مثل SoundCloud وبعض مستخرجات البودكاست)، ولاستنتاج أبعاد الفيديو (`VideoResolution`) من حقلي `width` و`height` الرقميين عند غياب نص `resolution`، ولدعم ترميزات `hev1` و`bytevc1` و`dvh1` كـ`VideoCodec.H265` (المستخدمة في TikTok وDouyin وBilibili وInstagram) و`vorbis` و`pcm`، وقراءة الحجم التقريبي `filesize_approx` ومعدل بت الصوت `abr` ومعدلات الإطارات العشرية (`23.98`/`29.97`/`59.94`).
  2. **معالجة القوائم والمنشورات متعددة الوسائط والروابط المباشرة في `DiscoveryResult.cs` و`Media.cs` و`DiscoveryService.cs`**:
     - حُدّث `Nickvision.Parabolic.Shared/Models/DiscoveryResult.cs` لتسطيح قوائم `entries` المتداخلة (`AddEntries`) وتمرير رابط المنشور الأصلي كرابط احتياطي (`fallbackUrl`) حتى تعمل المنشورات متعددة المقاطع (ألبومات Instagram وتغريدات X/Twitter متعددة الفيديو وBilibili) دون فقدان العناصر.
     - حُدّث `Media.cs` لتجنب اعتماد روابط البث المباشر المؤقتة (`.mp4`، `.m3u8`، `.mpd`، `.ts`، `.mp3`، `.m4a`...) كرابط صفحة للعنصر في القوائم، وتفضيل `webpage_url` أو `original_url` عليها حتى يعمل `yt-dlp` عبر المستخرج الأصلي للموقع مع الترويسات والكوكيز و`--playlist-items` بدل الفشل بخطأ `403 Forbidden` على رابط CDN منتهي الصلاحية، مع دعم قراءة الصور المصغرة من مصفوفة `thumbnails` والعناوين البديلة (`fulltitle` / `id`).
     - حُدّث `Nickvision.Parabolic.Shared/Services/DiscoveryService.cs` لتخطي أي أسطر نصية تسبق كائن JSON في مخرجات `yt-dlp` قبل التحليل.
  3. **استخلاص الروابط الذكي من نصوص المشاركة (`TryExtractHttpUri`) في الواجهتين**:
     - أُضيفت الدالة `TryExtractHttpUri` في `Nickvision.Parabolic.Shared/Helpers/StringExtensions.cs` ورُبطت في `Nickvision.Parabolic.WinUI/Views/MainWindow.xaml.cs` و`AddDownloadDialog.xaml.cs` و`Nickvision.Parabolic.GNOME/Views/AddDownloadDialog.cs` و`DiscoveryService.cs` لاستخراج رابط `http://` أو `https://` تلقائيًا عند لصق نص مشاركة كامل من تطبيقات الجوال أو المتصفح (مثل نصوص المشاركة من TikTok أو Instagram أو X أو Snapchat أو WhatsApp أو الروابط المحاطة بمحارف العزل ثنائي الاتجاه Unicode bidi isolates).
  4. **حماية عرض الصور المصغرة وترقية محرك Deno المرفق**:
     - حُدّث `Nickvision.Parabolic.Shared/Services/ThumbnailService.cs` و`AddDownloadDialog.xaml.cs` و`DownloadRow.xaml.cs` للتعامل الآمن مع فشل جلب الصور المصغرة أو صيغ الصور غير المدعومة من بعض المواقع والرجوع للصورة الافتراضية دون إسقاط عملية الاكتشاف أو التنزيل.
     - رُقّي إصدار Deno المدمج من `2.8.0` إلى `2.9.8` في `DenoExecutableService.cs` و`.github/workflows/windows.yml` و`flatpak/org.nickvision.tubeconverter.json` توافقًا مع متطلبات `yt-dlp 2026.10.21` (`deno>=2.9.8`) لحل تحديات JavaScript.

## طلب سابق: توحيد عرض عناصر الإعدادات، تصحيح موضع سهم التوسيع في RTL، وإصلاح تحميل مقاطع TikTok — 2026-10-24

- بعد إصدار `2026.5.139`، أرسل المستخدم ثلاث لقطات شاشة جديدة لصفحات الإعدادات ولخطأ تنزيل TikTok، وطلب:
  1. «مفروض حجم موحد لهم كلهم بدل ذي فوضى والاسهم مفروض تكون يمين الزر وليس يسار الزر»
  2. «وصلح دعم تحميل مقاطع تيك توك»
- الإذن الساري والنتيجة: رُفعت الإصلاحات في `11d332e6` و`020e04e3`، ونُشر الإصدار [BearDL 2026.5.141](https://github.com/2-sa/BearDL/releases/tag/2026.5.141) بنجاح من الكوميت `020e04e38c575e37383d9a723b48093897ff29e9` عبر [البناء 40100781992](https://github.com/2-sa/BearDL/actions/runs/40100781992) مع اكتمال جميع الفحوص والمنصات الست ورفع جميع الملفات الأحد عشر غير الفارغة.
- المشاكل المشخصة والإصلاحات المنفّذة:
  1. **توحيد عرض عناصر الإدخال والقوائم في صفحة الإعدادات (`SettingsPage`)**:
     - أُضيف `<Setter Property="MinWidth" Value="180"/>` إلى `PageComboBoxStyle` و`PageNumberBoxStyle` في `Nickvision.Parabolic.WinUI/Styles/InterfaceStyles.xaml`، مع `MinWidth="180"` لحقول النصوص (`TxtProxyUrl` و`TxtYtdlpDiscoveryArgs` و`TxtYtdlpDownloadArgs`) في `SettingsPage.xaml` لتظهر جميع القوائم المنسدلة وحقول الأرقام والنصوص بعرض موحد ومتناسق بدل تفاوت أحجامها حسب طول النص.
  2. **تصحيح موضع سهم التوسيع (`ExpandCollapseChevronBorder`) في بطاقات `SettingsExpander` بالواجهة العربية RTL**:
     - في قالب `CommunityToolkit.WinUI.Controls.SettingsExpander`، يضع `ExpanderHeader` زر التوسيع (`ExpandCollapseChevronBorder`) في العمود `1` (أقصى يسار البطاقة في RTL) بينما تبقى بطاقة `SettingsCard` الداخلية بمفتاح التبديل (`ToggleSwitch`) في العمود `0` على يمينه، مما يضع السهم يسار مفتاح التبديل ويدفع المفتاح إلى الداخل بعيدًا عن محاذاة بقية مفاتيح الإعدادات.
     - أُضيفت الدالة `ToggleSwitchHelper.ApplyExpanders` في `Nickvision.Parabolic.WinUI/Helpers/ToggleSwitchHelper.cs` واستُدعيت لبطاقات `RowUseAria` و`RowEmbedMetadata` و`RowEmbedThumbnails` في `SettingsPage.xaml.cs` لنقل `ExpandCollapseChevronBorder` في وضع RTL إلى العمود `2` داخل شبكة `PART_RootGrid` لبطاقة الترويسة وتحريك `PART_ContentPresenter` (مفتاح التبديل) إلى العمود `3`، فيصبح مفتاح التبديل في أقصى اليسار بمحاذاة جميع المفاتيح الأخرى ويكون سهم التوسيع على **يمين الزر** مباشرةً.
  3. **إصلاح خطأ `HTTP Error 403: Forbidden` عند تحميل مقاطع TikTok**:
     - السبب: حظر جدار الحماية في TikTok طلبات صفحات الويب الصادرة من `yt-dlp 2026.03.17` المرفق سابقًا مع التطبيق، إضافةً إلى عدم مطابقة روابط المشاركة القصيرة (`/share/video/` و`/@/video/`).
     - الإصلاح المزدوج:
       - ترقية إصدار `yt-dlp` المدمج من `2026.03.17` إلى `2026.10.21` في `Nickvision.Parabolic.Shared/Services/YtdlpExecutableService.cs`، و`.github/workflows/windows.yml`، و`.github/workflows/macos.yml`، و`flatpak/python3-modules.json`، وإضافة `2026.10.21` إلى مصفوفة فحوص `.github/workflows/plugins.yml`.
       - إضافة إضافة `resources/yt-dlp-plugins/tiktok_fix/yt_dlp_plugins/extractor/tiktok_fix.py` لتزويد مستخرجات `TikTokIE` و`TikTokUserIE` و`TikTokLiveIE` بترويسات عشوائية خالية من حروف العلة (`generate_blockbuster_headers`) مع تفعيل `impersonate=True` ودعم صيغ روابط `/share/video/` و`tiktokv.com` حتى في حال وجود نسخة `yt-dlp` أقدم، مع اختبارات وحدة غير متصلة بالشبكة في `tests/plugins/test_tiktok_fix.py`.

## طلب سابق: إصلاح انزياح الأشرطة العلوية وتفاصيل الإعدادات وقائمة المساعدة في RTL — 2026-10-24

- أرسل المستخدم خمس لقطات شاشة من النسخة العربية (الرئيسية الحديثة، وحلقة المفاتيح، والإعدادات، وقائمة المساعدة المنبثقة) وطلب «صلحها ثم سوي ابديت»، كما أكد أنه ألغى كوميتات `javhd` السابقة بالكامل عن عمد.
- الإذن الساري والنتيجة: شمل طلب المستخدم «صلحها ثم سوي ابديت» و«انت خربت ال RTL صلحه» رفع هذه الإصلاحات إلى `origin/main` وتشغيل مسار الإصدار (`release.yml`) لنشر تحديث جديد. نُشر الإصدار [BearDL 2026.5.139](https://github.com/2-sa/BearDL/releases/tag/2026.5.139) بنجاح من الكوميت `12e3dc690a1a8a8d8688ed89ed8a96d3651b00a0` عبر [البناء 40089604938](https://github.com/2-sa/BearDL/actions/runs/40089604938) مع اكتمال جميع الفحوص والمنصات الست ورفع جميع الملفات الأحد عشر غير الفارغة.
- المشاكل المشخصة والإصلاحات المنفّذة:
  1. **انزياح واختلاف عرض الأشرطة العلوية عن البطاقات السفلية في RTL وإصلاح اتجاه RTL الداخلي**:
     - في WinUI 3 عندما يكون العنصر الأب باتجاه `RightToLeft`، تُفسَّر القيمة الأولى `Margin.Left` على أنها الهامش **الأيمن** على الشاشة، وتُفسَّر القيمة الثالثة `Margin.Right` على أنها الهامش **الأيسر** على الشاشة. وبما أن `ScrollViewer` السفلية مضبوطة على `FlowDirection="LeftToRight"` مع `Padding="0,0,20,0"` لإبقاء شريط التمرير في أقصى يمين الشاشة، فإن كتابة `Margin="0,0,20,0"` ثابتة في XAML كانت تقتطع `20px` من **يسار** الشريط العلوي في RTL بدل يمينه.
     - تبيّن من لقطة المستخدم للإصدار `2026.5.137` أن ربط `FlowDirection="{Binding FlowDirection, ElementName=MainGrid}"` يفشل في WinUI 3 عندما تُضبط `MainGrid.FlowDirection` برمجيًا في الباني قبل اكتمال الشجرة المرئية، مما ترك الأشرطة العلوية في وضع LTR معكوسة الترتيب!
     - الإصلاح النهائي السليم: إزالة أي `FlowDirection="LeftToRight"` من الشبكات الأبوية (`WorkspaceDownloadsGrid`، وشبكات `SettingsPage.xaml` و`HistoryDialog.xaml` و`KeyringDialog.xaml`) بحيث ترث جميع الأشرطة والبطاقات اتجاه `RightToLeft` طبيعيًا من جذر الصفحة/النافذة، وإضافة الخاصية المرفقة `ScrollViewportHelper.ReserveScrollbarRight="20"` التي تضبط الهامش المنطقي تلقائيًا (`Margin.Left = base + 20` في RTL أي يمين الشاشة، و`Margin.Right = base + 20` في LTR أي يمين الشاشة) فتتطابق الحواف اليمنى واليسرى للمستطيل العلوي والسفلي بالبكسل مع الحفاظ الكامل على ترتيب RTL الأصيل.
  2. **عرض مربع اختيار لغة الترجمة (`CmbTranslationLanguage`) المفرط في الإعدادات**:
     - كان `CmbTranslationLanguage` يحمل `MinWidth="200"` مما خلق فراغًا كبيرًا بين كلمة «العربية» وسهم القائمة بخلاف بقية القوائم المنسدلة (`CmbTheme` و`CmbHistoryLength`). أُزيل `MinWidth="200"` (وكذلك `MinWidth = 160` من قائمة تصميم الواجهة في `GlassThemeHelper.cs`) ليتناسب عرض القائمة مع النص المختار بتناسق.
  3. **اختفاء بطاقتي «تصميم الواجهة» و«نسبة الشفافية» من صفحة الإعدادات**:
     - بعد تعديل `NavigationView_SelectionChanged` في `962cd34d` ليؤجل تعيين `ViewStack.SelectedIndex = index` عبر `DispatcherQueue.TryEnqueue`، صارت دالة `Page_Loaded` تُستدعى قبل إدراج صفحة `StackPanel` الأولى في الشجرة المرئية لـ`ViewStack`، فكانت `rowTheme.Parent` تُرجع `null` داخل `GlassThemeHelper.AttachSettingsCard` ويخرج التابع مبكرًا دون إضافة البطاقتين.
     - الإصلاح: تسمية الحاوية `x:Name="PanelUserInterface"` في `SettingsPage.xaml` وتمريرها مباشرةً إلى `GlassThemeHelper.AttachSettingsCard(PanelUserInterface, RowTheme, ...)` لضمان إدراج بطاقتي الشفافية وتصميم الواجهة دائمًا بعد بطاقة السمة.
  4. **ظهور شارة الاختصار العائمة `Ctrl+L` وسط صفحة الإعدادات**:
     - أُضيف `MainGrid.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;` في `MainWindow.xaml.cs` لمنع WinUI 3 من إظهار تلميحات KeyTip/ToolTip العائمة للاختصارات العامة (`Ctrl+L` و`Ctrl+V`) فوق الصفحات الداخلية.
  5. **موضع قائمة «المساعدة» المنبثقة (`FlyoutHelp`) في الشريط الجانبي الحديث**:
     - كانت `MenuFlyout` تستخدم `Placement="RightEdgeAlignedBottom"` فتنفتح في الواجهة العربية فوق عناصر الشريط الجانبي وتغطيها. ضُبط موضع `FlyoutHelp` ديناميكيًا عند الفتح (`TopEdgeAlignedRight` في RTL و`TopEdgeAlignedLeft` في LTR) لتنفتح بمحاذاة زر المساعدة دون حجب الشريط الجانبي.

## طلب سابق: توحيد صفحات Windows وتصحيح الترجمة والمحاذاة والنسخ — 2026-10-23

### الإذن والنتيجة

طلب المستخدم صراحةً «تمام ارفع وسوي ابديت»، ثم أضاف ملاحظات أثناء التسليم على زر تنزيل اقتراح الحافظة والنصوص غير المترجمة والمحاذاة ونسخ الملف. شمل الإذن رفع هذه التصحيحات وبناء تحديث ونشره. لم تُثبّت أدوات أو التطبيق على جهاز المستخدم، ولم يُشغّل بناء أو اختبار محلي.

نُشر [BearDL 2026.5.136](https://github.com/2-sa/BearDL/releases/tag/2026.5.136) بنجاح من `8ccbf57961444786514362c75ae7b1f4b0b6f9a9`، وهو وسم الإصدار المثبت عبر GitHub API. نجح [البناء 40004247391](https://github.com/2-sa/BearDL/actions/runs/40004247391) على Windows وmacOS وLinux بمعماريتَي كل منها. تحققنا من رفع الملفات الأحد عشر غير الفارغة: مثبّتا Windows، والنسختان المحمولتان، وحزمتا ZIP وDMG للماك، وحزمتا Flatpak، وSHA256SUMS.txt. حُدث وصف الإصدار بالتغييرات الفعلية وضُبط كالإصدار الأحدث.

### ما تغير

- موارد Windows المشتركة في `Styles/InterfaceStyles.xaml`: عناوين وبطاقات وحقول وأزرار متسقة تتبع ألوان السمة الحالية. أعيد تخطيط الإعدادات بتبويبات علوية، والسجل والحسابات ببحث وأوامر منفصلة عن نتائجها، وإضافة التنزيل بأقسام وتبويبات، و«عن البرنامج» ببطاقة هوية وأسطح مستقلة. شمل التنسيق الحوارات الفرعية وبطاقتي الشفافية واختيار التصميم الديناميكيتين. بقيت سلوكيات الحفظ والمفاتيح وحقول LTR والتراخيص والاعتمادات محفوظة.
- أزرار صف التنزيل صارت أيقونات 32×32 مع تلميحات وأسماء وصول؛ السجل داخل قائمة ⋯. يشمل ذلك زر النسخ بعد الاكتمال، وتظهر نتيجته دون توسيع البطاقة. قُيّد نص الحالة بالعرض المتاح، ومُنع تداخل نقرات النسخ وأُغلق تلميحه عند مغادرة الصف.
- زر «تنزيل» في اقتراح الحافظة يبدأ مباشرةً عند تفعيل Fast Download، وعند تعطيله يفتح الاكتشاف وخيارات التنزيل. يقرأ الإعداد عند النقر، ويستخدم مسار التجهيز الخلفي وفحص التكرار الحالي، ولا يستبدل النص المستقل في حقل الرابط.
- ثبت غياب `GlassThemeHelper.cs` من `resources/po/POTFILES`؛ أُدرج حتى تبقى ترجمات خيارات التصميم في كتالوج البناء. يفحص `tests/windows/Verify-TranslationCatalogs.ps1` وجود ترجماتها العربية في MO المنشور. تعرض قائمة اللغة أسماءها الأصلية من CultureInfo بدل الرموز، مع بقاء القيم المخزنة كما هي. وُحّدت حواف الأشرطة والقوائم بحجز مساحة التمرير اليمنى نفسها، ووُسّطت النصوص والأيقونات في صفوف الأوامر.
- الصورة السابقة لزر النسخ تثبت الفشل، لكنها لا تحدد الاستثناء الأصلي الذي أخفاه الكود. يستعمل الصف الآن `Helpers/FileClipboard.cs` لنقل قائمة مسارات Unicode بصيغة Shell `CF_HDROP` مع تأثير COPY ومالك نافذة صالح، وانتظار غير متزامن ومحاولات فتح محدودة وتحرير الذاكرة التي لم تنتقل ملكيتها إلى Windows. يسجل أسباب الفشل ويعرض رمز الخطأ عند حدوثه. يستمر النسخ بعد إغلاق المصدر دون الحاجة إلى WinRT StorageFile أو Clipboard.Flush.

### التحقق وحدوده

- نجح `git diff --check` وتحليل XML ومراجعة ربط العناصر. لم تُفقد العناصر المسماة أو مراحل ViewStack في الصفحات المعاد تخطيطها. هذا ليس وحده تجميع XAML أو قبولًا بصريًا.
- نجحت [الفحوص 40004052925](https://github.com/2-sa/BearDL/actions/runs/40004052925) والإملاء وسياسة ملفات البناء للمصدر المرفوع. يضم `tests/windows/ClipboardChecks.csproj` المساعد الفعلي، ويقرأ الملف عبر Shell على Windows x64 وARM64: نجح مسار عربي بمسافات وتأثير COPY؛ ورُفض ملف مفقود دون تغيير الحافظة؛ وبقيت البيانات بعد تدمير نافذة المصدر. تكررت الفحوص بنجاح في مسار الإصدار قبل تجميع المنصات. نجح أيضًا فحص الكتالوجات المنشورة على معماريتي Windows.
- لا يوجد إثبات بصري للنسخة المصححة أو تجربة لصق في تطبيق المستخدم بعد هذا الإصدار. المتبقي: صور RTL/LTR مع الفاتح والداكن والزجاجي، وضيق النافذة وDPI، وتجربة الصفحات والحوارات والمفاتيح والتمرير ولوحة المفاتيح، والتنزيل العادي والسريع والقوائم والتكرار، والنسخ واللصق في التطبيق المقصود والتحديث من نسخة سابقة. فحوص الحافظة لا تثبت وحدها تكامل كل تطبيق مستقبِل.
- تصميم GTK لم يتغير؛ عرض أسماء اللغات تعديل في المتحكم المشترك. تعطل تشغيل الماك التاريخي ما زال منفصلًا وغير مشخص؛ نجاح تغليفه لا يثبت إصلاح تشغيله.

### سجل التسليم لهذه المهمة

- `962cd34d`: تصميم الصفحات وأزرار التنزيل؛ نُشر 2026.5.134 بعد إعادة محاولة macOS Intel، التي تعثرت أولًا بمهلة اتصال CreateArtifact بعد نجاح تغليف DMG.
- `0fcaa5bd`: التنزيل من اقتراح الحافظة بضغطة واحدة. نجح بناء 2026.5.135، لكن إنشاء الإصدار توقف عند HTTP 403؛ لم يُعَد نشر نسخة أقدم بعد انتقال العمل إلى التصحيحات الجامعة.
- `8ccbf579`: الترجمة والمحاذاة ونسخ الملفات؛ نُشر 2026.5.136 بنجاح. لا تستخدم نتائج 134 أو135 وحدها للتحقق من هذه التصحيحات.

الجذر الفعلي هو مجلد مساحة العمل الحالي مباشرةً، على `main` و`origin` للمستودع `2-sa/BearDL`؛ تعليمات المجلد الداخلي في الوثائق التاريخية لم تعد تصف النسخة الحالية. أي طلب لاحق خارج هذه المهمة يحتاج إذنه الخاص؛ الإذن التاريخي ليس إذنًا دائمًا للنشر.

## طلب سابق: GitHub Actions failure review — 2026-10-20

After the investigation and local fixes, the user explicitly requested
"commit & push" for these changes. This authorizes committing and pushing the
reviewed fixes to `origin/main`, not manual release dispatch or installation on
the user's device. Historical delivery permissions below are not reusable
permission. The working tree was clean at `0eafa070` on `main` before this review.

- Inspected live Actions jobs and failed-step logs for release run
  [39280854704](https://github.com/2-sa/BearDL/actions/runs/39280854704), built from
  `0eafa070`. All six platform jobs failed; publication was skipped. The two
  subtitle jobs, duplicate-download job, and version job succeeded. The latest
  push checks and scheduled artifact-maintenance runs also succeeded.
- Windows x64 and ARM64: `CS0246` for `KeyboardAccelerator` and
  `KeyboardAcceleratorInvokedEventArgs` in the new Modern UI handlers. Added the
  missing `Microsoft.UI.Xaml.Input` import in
  `Nickvision.Parabolic.WinUI/Views/MainWindow.xaml.cs`. This also covers
  `FocusManager` used by the paste handler. The XAML compiler's `MSB3073` is the
  downstream failure, not evidence of a separate tool-installation problem.
- macOS and Linux, both architectures: `msgmerge` rejected four duplicate message
  identities in `resources/po/ar.po`: `Copy`, `Download`, `Customize`, and
  `Add a video, audio, or playlist URL to start downloading`. Removed the obsolete
  `Copy` entry and the three appended active duplicates, retaining one active
  translation per identity. Do not ignore obsolete entries when checking catalogs:
  `msgmerge` rejects their collision with an active entry too.
- Added a `translations` job to `.github/workflows/plugins.yml` using GNU
  `msgmerge` and `msgfmt` on all PO files without modifying them. Added PO files to
  its PR path filter. The release workflow already waits for this reusable
  workflow before all platform builds, so catalog errors now stop packaging early.
  Gettext installation is confined to the GitHub runner. Corrected `AGENTS.md` to
  describe the existing manual-only release trigger.
- Local validation: reproduced exactly the four catalog collisions from HEAD,
  then checked all 47 catalogs with a temporary context-aware identity scanner,
  including obsolete entries; no collisions remain. Verified retained Arabic
  translations, empty Arabic `translation-credits`, and the distinct `System`
  contexts. All seven workflow YAML files parsed with the existing YAML library;
  the new shell steps passed `bash -n`, and release dependency/path checks passed.
  All seven artifact-policy tests passed. `git diff --check` passed.
- Limits: GNU gettext tools and a .NET SDK are not available locally; no software
  was installed, no application build was run, and no new Actions run was started.
  Source checks address the observed failures but do not prove a complete build
  or runtime/visual correctness. After explicit delivery/build authorization,
  run the new checks and all six platform jobs on the changed commit, then verify
  artifacts and runtime behavior. The macOS startup report remains separate and
  unresolved. Older failures (including the write-only theme property and FFmpeg
  404) were superseded by successful later builds; they are not new failures in
  the reviewed run.

## Previous requests and historical evidence

## الطلب الحالي: إصلاح الوضع الفاتح (Light Mode) وإضافة الوضع الزجاجي (Glass Mode)

### 1. إصلاح خلفية الوضع الفاتح في Windows
- أرسل المستخدم صورة من النسخة العربية (تُظهر عودة العربية وRTL واسم العرض `BearDL` بعد إصدار `2026.5.124`)، وفيها نصوص الواجهة سوداء وبطاقة التنزيل فاتحة بينما خلفية النافذة الرئيسية ما زالت داكنة، فصارت عناوين القوائم والشريط الجانبي شبه غير مرئية، وطلب: «في مشكلة ب light mode صلحها».
- السبب المثبت في الكود: في `Nickvision.Parabolic.WinUI/Views/MainWindow.xaml` أُحيطت `MainGrid` بشبكة جذرية خارجية `<Grid FlowDirection="LeftToRight">` لإبقاء إحداثيات مناطق النقر لشريط العنوان باتجاه LTR، فأصبحت تلك الشبكة الخارجية هي `Window.Content`. في WinUI 3 تراقب `<MicaBackdrop/>` (`Window.SystemBackdrop`) و`PopupRoot` خاصية `ActualTheme` لجذر `Window.Content` مباشرةً ولا تتتبع العناصر الفرعية، بينما `MainWindow.xaml.cs` كان يضبط `MainGrid.RequestedTheme` فقط؛ فبقيت خلفية Mica على سمة نظام Windows الداكنة رغم تحوّل عناصر `MainGrid` إلى الوضع الفاتح.
- الإصلاح المحلي: سُمّيت الشبكة الجذرية `x:Name="WindowRoot"` في `MainWindow.xaml`، وجُمعت تهيئة السمة في `ApplyTheme()` داخل `MainWindow.xaml.cs` لتضبط `WindowRoot.RequestedTheme` و`MainGrid.RequestedTheme` و`AppWindow.TitleBar.PreferredTheme` معًا عند بدء التشغيل وعند حفظ إعداد `Theme`.

### 2. إضافة الوضع الشفاف الزجاجي المستوحى من تصاميم أبل (`Theme.Glass` / «زجاجي») والتحكم بنسبة الشفافية
- طلب المستخدم: «ابيك تضيفلي وضع شفاف زجاجي مستوحى من تصاميم ابل يكون مختلف كليا عن التصميم الحالي ولو تقدر بقدر الامكان تفصله بملفات خاصه فيه لسهوله ازالته لاحقا»، ثم بعد تجربة النسخة الأولى الشفافة جدًا (`b91c3c22`) طلب جعل الشفافية الافتراضية **50%** («اعتقد بالغنا بالشفافيه كثيرا خلنا نقول شفاف بنسبة 50%») مع إضافة شريط تحكم في الإعدادات («هل تقدر تخليني من الاعدادات اتحكم بشفافيه؟»).
- الملفات المعزولة للوضع الزجاجي (لتسهيل تعديله أو إزالته لاحقًا):
  - `Nickvision.Parabolic.WinUI/Helpers/GlassThemeHelper.cs`: ملف مستقل يحوي كامل منطق الوضع الزجاجي لـWinUI 3 (`ClearGlassBackdrop` بشفافية نافذة افتراضية `50%` عبر DWM و`CompositionColorBrush`، مع شريط تحكم حي `AttachSettingsCard` في الإعدادات لضبط «نسبة الشفافية» بين `5%` و`95%` وحفظها تلقائيًا في `glass-transparency.txt`، وإطار زجاجي داخلي لامع، وقاموس الموارد العاكس للبطاقات والأزرار وحواف اللمعان `Specular Borders`).
  - `Nickvision.Parabolic.GNOME/Helpers/GlassThemeHelper.cs` و`Nickvision.Parabolic.GNOME/Resources/glass-theme.css`: ملفان مستقلان يحويان منطق وتنسيقات `.glass-mode` لواجهة GTK4 / libadwaita على الماك ولينكس.
- نقاط الربط المحدودة في الملفات القائمة:
  - `Nickvision.Parabolic.Shared/Models/Theme.cs` و`Controllers/PreferencesViewController.cs` و`resources/po/ar.po`: إضافة قيمة `Glass` وترجمتها العربية `زجاجي` ونصوص شريط `نسبة الشفافية`.
  - `Nickvision.Parabolic.WinUI/Views/MainWindow.xaml.cs` و`SettingsPage.xaml.cs`: استدعاء `GlassThemeHelper.Apply(...)` في `ApplyTheme()` و`GlassThemeHelper.AttachSettingsCard(...)` في `Page_Loaded`.
  - `Nickvision.Parabolic.GNOME/Views/MainWindow.cs` و`PreferencesDialog.cs` و`Resources/org.nickvision.tubeconverter.gresource.xml` و`org.nickvision.tubeconverter.css`: استدعاء `GlassThemeHelper.Apply(...)` واستيراد `glass-theme.css`.

### 3. استعادة التصميم القديم كافتراضي وإضافة خيار التبديل بين التصميم القديم والجديد في الإعدادات
- بعد تجربة الإصدار [`2026.5.129`](https://github.com/2-sa/BearDL/releases/tag/2026.5.129) طلب المستخدم: «رجع التصميم القديم وحط خيار ب الاعدادات للانتقال للتصميم القديم او الجديد».
- التغييرات المنفذة محليًا في `Nickvision.Parabolic.WinUI/Helpers/GlassThemeHelper.cs` و`MainWindow.xaml` و`MainWindow.xaml.cs` و`resources/po/ar.po`:
  - **استعادة التصميم القديم (`Classic`) كوضع افتراضي**: يفتح التطبيق افتراضيًا على التصميم الأصلي الكامل (شريط القوائم العلوي `ملف / تعديل / عرض / التنزيلات / المساعدة` + شاشة البداية `Pages.Home` + شريط أدوات التنزيلات الكلاسيكي).
  - **إضافة خيار «تصميم الواجهة» (`Interface Layout`) في الإعدادات**: بطاقة إعدادات جديدة تحت السمة تتيح الاختيار بين **«القديم (الكلاسيكي)» (`Classic`)** و**«الجديد (الحديث)» (`Modern`)**، مع حفظ الاختيار تلقائيًا في `ui-layout-mode.txt` والتبديل الفوري الحيّ (`ApplyLayoutMode`) دون الحاجة لإعادة تشغيل البرنامج.
- أذن المستخدم صراحةً برفع الكوميت وإصدار التحديث بعد مراجعة استهلاك الذاكرة وحجم التطبيق.

### 4. إصلاح انهيار التصميم الحديث (SelectionChanged) وإعادة تصميمه جذرياً مع إضافة زر نسخ الملف
- واجه المستخدم انهياراً عند اختيار التصميم الحديث في `2026.5.130`، بسبب `Child collection must not be modified during measure or arrange` عند إطلاق `SelectionChanged` في مرحلة قياس وتخطيط `NavigationView`. تم تأجيل استدعاء `ViewStack.SelectedIndex` و`UpdateDownloadsList` عبر `DispatcherQueue.TryEnqueue` لمنع الانهيار.
- طلب المستخدم إعادة تصميم الواجهة الحديثة بشكل عملي وغير مكرر مع إضافة زر لنسخ الملف المنتهي دون فتح مجلده:
  - إضافة زر «نسخ» (`BtnCopyFile` / `CopyFile`) في صفحة التنزيلات المكتملة داخل [DownloadRow.xaml](file:///c:/Users/PC/Documents/ChatGPT/BearDL/BearDL/Nickvision.Parabolic.WinUI/Controls/DownloadRow.xaml) لنسخ الملف الحقيقي كعنصر تخزين في الحافظة (`DataPackage.SetStorageItems`) لسهولة لصقه فوراً في برامج أخرى كالمحادثات والمحررات، مع إشعار بصري مؤقت بحالة النسخ («تم النسخ»).
  - إزالة التكرار الكامل في [MainWindow.xaml](file:///c:/Users/PC/Documents/ChatGPT/BearDL/BearDL/Nickvision.Parabolic.WinUI/Views/MainWindow.xaml):
    - شريط أوامر موحد للرابط السريع (`QuickUrlCard`) يحتوي حقل الإدخال وزر التخصيص وزر التحميل السريع المباشر.
    - اقتراح ذكي لرابط الحافظة (`ClipboardSuggestion`) يظهر تلقائياً عند نسخ رابط فيديو لتنزيله بضغطة زر واحدة أو تجاهله.
    - اختصارات لوحة المفاتيح: `Ctrl+L` للتركيز على شريط الرابط، `Ctrl+V` للصق وتجهيز الرابط من أي مكان في النافذة، و`Enter` للتنزيل السريع.
    - دعم السحب والإفلات (`Drag & Drop`) لروابط الوسائط مباشرة في مساحة العمل.
    - حالة فارغة حديثة وواضحة (`ModernEmptyState`) مع إرشادات البدء السريع.
    - شريط أدوات التنزيلات السياقي (`ModernDownloadsToolbar`): يعرض عنوان الفلتر والعدد وأزرار الإجراءات الجماعية (إيقاف الكل، إعادة محاولة الفاشلة، مسح المنتظرة، مسح المكتملة) فقط عند وجود عناصر مناسبة لكل إجراء.
    - استبدال زر «عن» في الشريط الجانبي بزر «المساعدة» (`Help`) الذي يفتح قائمة منبثقة كاملة (فحص التحديثات، المستودع، التبليغ، المناقشات، عن).
  - إضافة الترجمات العربية الكاملة في [ar.po](file:///c:/Users/PC/Documents/ChatGPT/BearDL/BearDL/resources/po/ar.po).

## المتابعة السابقة: اختفاء اللغات بعد تغيير اسم العرض

- أرسل المستخدم صورة لقائمة لغة لا تعرض إلا System وen_US، وأكد أن طلبه تغيير الاسم الظاهر فقط، ثم طلب مراجعة بقية تغييرات التسمية وتصحيحها. أذن المستخدم صراحةً: «ارفعه وسوي نسخة»، ورُفع التصحيح في `c17c224d` ونُشر الإصدار [2026.5.124](https://github.com/2-sa/BearDL/releases/tag/2026.5.124) من [البناء 38990949083](https://github.com/2-sa/BearDL/actions/runs/38990949083).
- السبب المثبت في كود Nickvision.Desktop: `TranslationService` يشتق مجال gettext من `AppInfo.EnglishShortName`، وهو المعامل الثالث للمنشئ، وليس من خاصية ShortName الظاهرة. تغيير ذلك المعامل إلى BearDL في `e14434f1` جعله يبحث عن `beardl.mo`، بينما `Nickvision.Common.targets` يولد `parabolic.mo`. لذلك لم يكتشف اللغات؛ ملفات PO العربية لم تُحذف.
- أُعيد المعامل الثالث إلى `Parabolic` مع تعليق يشرح الارتباط، وبقي `_appInfo.ShortName = "BearDL"` في المتحكم. الهوية الداخلية والترجمة ثابتتان، والاسم الظاهر BearDL. نجاح [البناء السابق](https://github.com/2-sa/BearDL/actions/runs/38916416609) لا يثبت سلامة تحميل اللغات؛ لم يكن يحوي فحصًا لذلك.
- روجع فرق كوميت التسمية: واجهتا WinUI وGTK، ومفاتيح PO، وإضافتا المتصفح، وبيانات سطح مكتب Linux والمثبّت. بقيت مسارات البيانات وApplication ID وGUID وأسماء الملفات التنفيذية والبروتوكول ومفاتيح الإضافتين كما كانت. اسم التثبيت الظاهر منفصل عن مجلده الداخلي القديم؛ لم يُكتشف خلل داخلي إضافي في بقية فرق التسمية.
- أُضيف `tests/windows/Verify-TranslationCatalogs.ps1` وخطوة بعد Publish وقبل Installer على معماريتي Windows. تفحص الخطوة وجود كتالوج لكل لغة في LINGUAS بالمجال الذي تشتقه المكتبة من هوية التطبيق، وصحة ترويسة MO، ووجود نص الإعدادات العربي. نجح الفحص الجديد على معماريتي Windows في البناء `38990949083`، وأثبتت صورة المستخدم في 2026-10-18 ظهور العربية واتجاه RTL واسم `BearDL` أثناء التشغيل.

## الطلب الحالي: اسم العرض BearDL فقط

- طلب المستخدم تغيير النصوص الظاهرة إلى `BearDL` دون إعادة تسمية الكود أو المعرّفات. أذن المستخدم بإنشاء كوميت ثم رفعه. يشمل الإذن تعديل اسم العرض الحالي، ويبدأ الرفع بناء Actions تلقائيًا. لا يشمل الإذن تنزيل أدوات على جهاز المستخدم.
- أصبح `AppInfo.ShortName` الظاهر هو `BearDL` في المتحكم المشترك، وتستعمله عناوين WinUI وGTK وحوارات «عن» ورسائل التحديث. حُفظ `AppInfo.Name` القديم لأنه يحدد مجلدات البيانات والسجلات والترحيل. تغيير معامل التهيئة الإنجليزي السابق تراجع عنه تصحيح 2026-10-17 أعلاه لأنه يحدد مجال الترجمة داخليًا.
- حُدثت النصوص الثابتة في الواجهتين: الاستعادة، وإخلاء المسؤولية، والتحديثات التجريبية، وشرح التنزيل الفوري وقائمة «عن». حُدثت مفاتيحها وترجماتها الموجودة في ملفات PO، مع إبقاء العلامة `BearDL` كما هي.
- حُدث اسم العرض في مثبّت Windows واختصاراته وإزالة التثبيت وبيانات سطح مكتب Linux، ونصوص إعدادات إضافة Chrome وFirefox وقوائمهما ووصفهما. يفصل المثبّت الآن الاسم الظاهر عن مجلد التثبيت القديم، ويحذف فقط اختصاريه القديمين عند الترقية قبل إنشاء الاسم الجديد. حزمة الماك تستخدم اسم BearDL بالفعل.
- لم تتغير namespaces أو أسماء المشاريع والملفات التنفيذية أو Application ID أو GUID المثبّت أو بروتوكول `parabolic://` أو مفاتيح الأوامر والتخزين. نسب المشروع وتراخيصه وروابط المصدر الأصلية محفوظة.
- نجح تحليل ملفات PO الـ47 دون مفاتيح مكررة، وتحليل XML لبيانات Linux وJSON للإضافتين وفحص تركيب JavaScript وفحص `git diff --check`. قورنت بيانات الإضافتين قبل التعديل وبعده وتطابقت عدا الاسم والوصف، وثبت بقاء GUID والبروتوكول ومجلد تثبيت Windows. لم يُبنَ التطبيق ولم يجرِ تحقق بصري. يلزم بعد التسليم المأذون التحقق من العناوين والرسائل والاختصارات في النسخة المثبتة على Windows وGTK، وتجربة ترقية Windows دون فقد البيانات. فحوص النصوص والموارد المحلية لا تثبت المظهر أثناء التشغيل.

## الطلب الحالي: خطأ PhantomJS عند اكتشاف الوسائط

- متابعة التسليم: فشل بناء [38717552516](https://github.com/2-sa/BearDL/actions/runs/38717552516) للكوميت `3559c29d` في اختبارات الإضافات على المحرك المرفق والأحدث. نجحت الحالات العشر الجديدة؛ فشل استيراد مجموعة الترجمة السابقة برسالة `ModuleNotFoundError`. السبب أن كل ملف اختبار كان يضيف مسار إضافته وحده، بينما yt-dlp يثبّت مسار namespace عند أول استيراد. أُضيف `tests/plugins/plugin_paths.py` لتسجيل جميع جذور الإضافات قبل أي استيراد في الملفين. الفحص المحلي السابق عبر المحرك المدمج كان يحمّل جميع الإضافات مسبقًا، فلم يمثّل هذا العزل. رُفع التصحيح في `4ce4ec2b` ضمن متابعة التسليم المأذون. نجحت مجموعتا الاختبارات على المحرك المرفق والأحدث، وفحص التكرار، في [البناء الجديد 38718559323](https://github.com/2-sa/BearDL/actions/runs/38718559323)، وبدأت وظائف المنصات الست. التغليف والنشر لم يكتملَا عند هذا الفحص. هذه نتيجة محلية بعد الرفع؛ لا يبدأ إصدار آخر لمجرد تحديث حالة التوثيق.

- أذن المستخدم الآن بكوميت الإصلاح ورفعه، مع تجنب اسم الموقع في رسالة الكوميت والتوثيق الوصفي. سيبدأ الرفع بناء Actions تلقائيًا. كان HEAD المحلي `d1c4f25c` عند بدء العمل؛ حُفظ إصلاح تغليف الماك الموجود فيه.
- تقرير المستخدم: `yt-dlp 2026.8.19`، وDeno `2.9.7` وFFmpeg `n8.1.3-18-g46d899d7a0-20261014` وaria2 `1.37.0`. الخطأ يطلب PhantomJS، فلا يصح إرجاعه تلقائيًا إلى محرك قديم أو اقتراح تثبيته دون اختبار.
- أُعيد إنتاج صفحة التحقق: HTML قصير يحتوي `go()` و`leastFactor` وحسابات مشوشة بالتعليقات ثم تعيين كوكي KEY وإعادة التحميل. مستخرج yt-dlp يستدعي PhantomJS لهذا المسار حتى في المصدر الحديث.
- أُضيف `resources/yt-dlp-plugins/cookie_challenge/yt_dlp_plugins/extractor/cookie_challenge.py` لمعالجة الصيغة المرصودة بقراءة قواعد حسابية محددة، دون eval أو تنفيذ JavaScript أو تشغيل متصفح. حدود: 64 KiB للصفحة، 128 عملية، أحجام أعداد محدودة، ومليون خطوة تحليل عوامل كحد أقصى. كوكي KEY/RNKEY مقيد بنطاق الموقع، وإعادة طلب واحدة فقط. أي صيغة غير معروفة تُرفض صراحةً.
- يحمّل التطبيق الإضافة عبر مسار plugins الحالي، وتنسخها أهداف التغليف الموجودة للمنصات كلها؛ لا حاجة إلى تغيير C# أو إضافة خدمة أو مؤقّت خلفي.
- أضيفت اختبارات `tests/plugins/test_cookie_challenge.py`: الحساب والتعليقات وفرعا الشرط والإزاحة الموقعة والأعداد الزوجية والأولية، رفض المدخلات الكبيرة والكود غير المعروف، ثبات خيارات الطلب، منع حلقات الإعادة ومنع تسريب الكوكي إلى نطاق آخر.
- نجحت الاختبارات العشرة الجديدة، ثم مجموعة الإضافات كاملة (15 اختبارًا)، محليًا باستخدام Python المدمج داخل yt-dlp الموجود، دون تنزيل Python أو أدوات. احتاج مشغّل الاختبارات المؤقت إلى إعادة مرجع XHamsterIE الأصلي قبل اختبار الإضافة السابقة، لأن تحميل إضافات المحرك التلقائي يستبدله؛ فشل اختباران قبل ضبط المشغّل ونجحا بعده دون تعديل كود الإضافة السابقة. الاختبار الحي عبر جلسة HTTP أعاد صفحة تحقق أولًا، ثم قبل الكوكي المحسوب وأعاد HTTP 200 وصفحة عادية دون التحدي. تجربة HTTP مباشرة بالمحرك المرفق القديم `2026.03.17` أعادت 410 قبل التحدي؛ لا تخف هذه النتيجة ولا تخلطها باختبار قبول الكوكي الناجح.
- لم يُنزّل فيديو كامل، ولم يُبنَ إصدار أو تُعدّل النسخة المثبتة، ولم تُتحقق جلسة التنزيل الكاملة بمحرك المستخدم `2026.8.19`. الاختبارات لا تثبت دعم صيغ تحديات مختلفة أو صفحات الحسابات أو القيود الجغرافية. يعمل مسار CI الحالي على فحوص الإضافة الجديدة عند التسليم المأذون لاحقًا.

## خريطة التوثيق

| الملف | الغرض |
| --- | --- |
| `AGENTS.md` | تعليمات المستودع وحدود العمل |
| [PROJECT_AND_COMMIT_GUIDE.md](PROJECT_AND_COMMIT_GUIDE.md) | شرح المشروع ومسار التنزيل والكوميتات الـ22 |
| [RELEASING.md](RELEASING.md) | البناء والحزم والترقيم والنشر |
| [RESOURCE_REVIEW_2026-10-14.md](RESOURCE_REVIEW_2026-10-14.md) | مراجعة الموارد وقياساتها وحدود التحقق |
| هذا الملف | حالة التسليم، تفاصيل التغييرات، المشكلات المعلّقة وخطوات الاستمرار |

## Git والإذن الساري

- مستودع Git داخل `BearDL/` الداخلي، و`origin` هو `https://github.com/2-sa/BearDL`، والفرع `main`. المسارات في الوثائق نسبية من جذر المستودع؛ لا تذكر مسارات جهاز المستخدم.
- آخر كوميت مرفوع هو `dab74e117926c2a5cc46855ea815dcd967eb17ca`: إصلاح التباس مؤقّت WinUI والتوثيق المرتب، بعد `bab225fe` وكوميت المميزات `fcc9d3e8`.
- أذن المستخدم الآن صراحةً: «صلح مشكلة الويندوز وارفع الكوميت». يشمل التسليم إصلاح خطأ تجميع Windows والتوثيق المحلي المرتب، ويبدأ الرفع بناء Actions الجديد تلقائيًا. هذا الإذن لا يشمل تثبيت أدوات على جهاز المستخدم.
- افحص `git status --short` و`git diff` قبل أي تعديل، واحفظ التوثيق المحلي والدليل الجديد. لا تعمل reset أو حذفًا للتغييرات المعلّقة. لا تثبّت برامج أو أدوات أو شهادات محليًا دون إذن.
- لا تُعد تنظيم مجلدات المشاريع أو تنقل ملفات الكود لمجرد ترتيب Markdown؛ مسارات المشروع وموارده والتغليف مترابطة.
- دُمجت وثيقة التسليم المؤرخة في هذا الملف قبل حذفها؛ كان هناك تداخل في الغرض والمعلومات، لا تطابق حرفي لكل المحتوى. أُبقيت أدلة انهيار الماك ومواضع التعديلات والفحوص وخطوات الاستمرار. ملف `ai.md` الخارجي باقٍ لكنه اختُصر إلى نقطة دخول بدل تكرار التعليمات.
- طلب المستخدم README عربيًا بالكامل باتجاه RTL. عُرّبت عناوينه ونصوصه وحُفظت أسماء الملفات التقنية، مع حاوية `dir="rtl"` ومحاذاة يمين وعزل LTR للمعرّفات. جُرّب معالج Markdown في GitHub دون نشر: يحذف `bdi` لكنه يحتفظ بـ`span dir="ltr"` و`code dir="ltr"`، لذلك استُخدمت الوسوم المدعومة. نجح فحص HTML الناتج والجداول وأسماء الحزم؛ رُفع الملف ضمن `dab74e11`.

## حالة الإصدارات والفحوص: تحقق مباشر في 2026-10-14

| الإصدار | الكوميت | النتيجة |
| --- | --- | --- |
| [2026.5.116](https://github.com/2-sa/BearDL/releases/tag/2026.5.116) | `567a9e37` | منشور ومستقر؛ يتضمن ZIP وDMG للماك، ولا يتضمن تحسينات `fcc9d3e8` |
| [بناء 2026.5.117](https://github.com/2-sa/BearDL/actions/runs/38578179611) | `fcc9d3e8` | كشف خطأ استيراد `System.IO` المطلوب لـ`Path`؛ صُحح وأُلغي البناء المستبدل |
| [بناء 2026.5.118](https://github.com/2-sa/BearDL/actions/runs/38578731459) | `bab225fe` | نجحت الاختبارات وmacOS وLinux بمعماريتيهما؛ فشل Windows بمعماريتيه، وتُجاوز النشر. لا يوجد إصدار منشور بهذا الرقم عند الفحص |

### إصلاح تجميع Windows

الخطأ `CS0104` في `Nickvision.Parabolic.WinUI/Views/MainWindow.xaml.cs` عند تعريف `_notificationTimer`: الاسم `DispatcherQueueTimer` ملتبس بين `Microsoft.UI.Dispatching.DispatcherQueueTimer` و`Windows.System.DispatcherQueueTimer` بسبب الاستيرادين.

صُحح الاستيراد باستخدام alias يربط `DispatcherQueueTimer` بنوع `Microsoft.UI.Dispatching` صراحةً، مع إبقاء `Windows.System` اللازم لفتح الروابط. لا يغيّر التصحيح مهلة الإشعارات أو عدد المؤقّتات. نجحت خطوة `Publish` لتجميع Windows x64 وARM64 في [بناء 2026.5.119](https://github.com/2-sa/BearDL/actions/runs/38609883409) من `dab74e11`. نجح إنشاء مثبت x64 ورُفعت حزم المثبت والنسخة المحمولة، بينما ARM64 في إنشاء المثبت وLinux ما زال قيد البناء عند آخر فحص. نجحت الاختبارات وبناء macOS بمعماريتيه. لم يكتمل نشر الإصدار بعد، ولم يُختبر التطبيق أثناء التشغيل. هذا تحديث حالة محلي بعد الرفع؛ لا ترفع كوميتًا جديدًا لمجرد نشر هذه النتيجة دون طلب جديد.

### نتائج مراجعة الموارد

- روجع النطاق `1118e6a3..567a9e37`، وهو 22 كوميتًا، مع التعديلات الأحدث.
- فحص التكرار يستعلم عن روابط الطلب بفهرس URL ودفعات من 256 معاملًا، بدل تحميل السجل كله. يفحص كل ملف ذي صلة مرة واحدة لكل URI مختلف.
- تجهيز الطلبات السريعة محدد باثنين. تحديث مناطق شريط العنوان يجمع أحداث التخطيط ويعيد استخدام القائمة، وقيود التمرير لا تعيد كتابة العرض دون تغيير. يفك إغلاق InfoBar معالج زر التفاصيل.
- نجحت الحالات التسع للكاشف وفحص الموارد محليًا بأدوات موجودة وعلى Actions. عينة 10,000 رابط متكرر خصصت 552 بايت وفحصت ملفًا واحدًا؛ استعلام على 50,000 صف أعاد صفًا مطابقًا باستعمال الفهرس. التفاصيل في تقرير الموارد.
- لم يُقَس CPU أو RSS أو GPU للتطبيق كله، ولم تُختبر الواجهات بصريًا. نجاح GTK لا يثبت حل انهيار الماك على جهاز المستخدم.

## الميزات التي سُلّمت في المصدر

التفاصيل التالية تخص المصدر المرفوع في `fcc9d3e8` وتصحيح `bab225fe`. نجح تجميع GTK في البناء السابق، ثم نجح تجميع Windows بمعماريتيه بعد تصحيح `dab74e11`. لا تعتبر وصف السلوك تحققًا أثناء التشغيل.

## جميع إشعارات التطبيق: خمس ثوانٍ

طلب المستخدم إغلاق جميع الإشعارات بعد خمس ثوانٍ، وليس إشعار «إعداد التنزيلات في الخلفية» وحده.

- `Nickvision.Parabolic.WinUI/Views/MainWindow.xaml.cs`: مؤقّت `DispatcherQueueTimer` واحد، غير متكرر، بفاصل خمس ثوانٍ. يُوقف قبل الإشعار الجديد ثم يبدأ له من جديد؛ يُوقف عند إغلاق InfoBar أو النافذة.
- `Nickvision.Parabolic.GNOME/Views/MainWindow.cs`: `toast.Timeout = 5` لكل إشعار، مع إزالة الاستثناءات `Timeout = 0` للتحديثات وتفاصيل الأخطاء.
- لا ينطبق المؤقّت على حوارات التأكيد التي تنتظر قرار المستخدم. يعتمد إصدار GTK على سلوك `Adw.Toast` الأصلي، ويلزم اختبار المهلة أثناء التفاعل معه.

اختبر وصول إشعارين متتابعين، والإغلاق اليدوي، وأزرار التحديث والتفاصيل، وكل درجات الإشعار، وإغلاق التطبيق قبل انقضاء المؤقّت.

## اختيار مجلد الحفظ في صفحة التنزيل السريع

طلب المستخدم إظهار اختيار الوجهة في صفحة الرابط عند تفعيل التنزيل السريع.

- WinUI: `Views/AddDownloadDialog.xaml` وملف C#، والعناصر `RowFastSaveFolder` و`TxtFastSaveFolder` و`BtnFastSelectSaveFolder`. تظهر في الوضع السريع فقط أسفل زر ملف الروابط، وتعرض آخر مجلد. ينتقي الزر المجلد عبر `FolderPicker` المرتبط بـ`WindowId`.
- GTK: `Blueprints/AddDownloadDialog.blp` و`Views/AddDownloadDialog.cs`، والعناصر `fastSaveFolderRow` و`fastSelectSaveFolderButton`، مع `Gtk.FileDialog.SelectFolderAsync`.
- Shared: أُضيف المعامل الاختياري `selectedSaveFolder` إلى `AddFastDownloadsAsync`. تُلتقط الوجهة قبل اكتشاف الوسائط في الخلفية؛ الوجهة المعروضة تتقدم على اقتراحات مجلدات ملف الروابط. تحفظ قوائم التشغيل في مجلد فرعي بعنوان القائمة.
- تُحفظ الوجهة في `PreviousSaveFolder` بعد إضافة التنزيلات بنجاح، مع `transaction.CommitAsync()`. كان استدعاء Commit ناقصًا في التعديل المحلي الأول، وأُضيف أثناء تنفيذ فحص التكرار.
- إلغاء منتقي المجلد لا يغيّر الاختيار، وإلغاء حوار الرابط لا يحفظ وجهة جديدة. عنوان Windows عربي والمسار LTR، وزر المجلد له اسم لقارئ الشاشة.

اختبر الرابط المفرد، وقائمة التشغيل، وملف روابط له وجهات مقترحة، والمجلد ذي الاسم العربي، وإلغاء المنتقي، ثم فتح حوار جديد بعد تنزيل ناجح للتحقق من تذكّر الوجهة.

## الصياغة العربية

اعترض المستخدم على «في قائمة الانتظار» وعدّها صياغة حرفية. عُدلت الترجمات النشطة في `resources/po/ar.po`:

| المفتاح | الترجمة الحالية |
| --- | --- |
| `Queued` | بانتظار التنزيل |
| `Clear All Queued` | مسح التنزيلات المنتظرة |
| `An error occurred while clearing queued downloads` | تعذّر مسح التنزيلات المنتظرة |

طبّق مهارة `arabic-ui` في أي نص عربي جديد. لا تكتفِ بترجمة الإنجليزية كلمة بكلمة. لم تُغيّر الأسطر القديمة المعلّقة بـ`#~`؛ لا تظهر في التطبيق. العبارات الجديدة لتأكيد التكرار وإعداده مترجمة أيضًا. استخدم مفتاح `Download Again` الموجود مسبقًا وترجمته «إعادة التنزيل»؛ لا تنشئ مدخل PO ثانيًا له.

## التنبيه قبل تكرار التنزيل

طلب المستخدم التنبيه للرابط الجاري أو لملف سابق موجود، ثم طلب خيار تفعيل وتعطيل في الإعدادات.

### الإعداد والتأكيد

- المفتاح المحفوظ `WarnBeforeDuplicateDownload`، وقيمته الافتراضية `true`؛ الوصول إليه عبر `IConfigurationServiceExtensions` و`PreferencesViewController`.
- عنوان الخيار «التنبيه قبل تكرار التنزيل»، في قسم التنزيلات في واجهتَي الإعدادات. مفتاح WinUI يدخل أيضًا في `ToggleSwitchHelper.Apply` وله اسم وصول، وتُحفظ قيمته مع الإعدادات الأخرى. GTK يحفظه عند إغلاق التفضيلات بالطريقة الموجودة سابقًا.
- يعرض التأكيد سبب التكرار والرابط والمسار عند وجود ملف. الخياران «إعادة التنزيل» و«إلغاء»، والإلغاء هو الافتراضي. الإلغاء يرفض الطلب كاملًا ولا يضيف تنزيلات أو ملف M3U؛ لا توجد حاليًا إمكانية متابعة العناصر الجديدة فقط.
- تعطيل الإعداد يتجاوز فحص التكرار والتأكيد. لا يغلق مؤقّت الإشعارات نافذة التأكيد.

### مواضع التنفيذ

| الملف | دوره |
| --- | --- |
| `Shared/Models/DuplicateDownload.cs` | ملف جديد؛ أسباب التكرار وكاشف مستقل، مع فحص `File.Exists` للملف السابق |
| `Shared/Services/IDownloadService.cs` و`DownloadService.cs` | `AddWithDuplicateCheckAsync`، وفحص السجل والتنزيلات الجارية والمنتظرة والمكتملة؛ إرجاع قرار الإضافة |
| `Shared/Controllers/AddDownloadDialogController.cs` | مرور الإضافة السريعة والعادية والقوائم بالفحص؛ بناء رسالة التأكيد وإرسالها إلى الواجهة عبر `ConfirmDuplicateDownloadAsync` |
| `WinUI/Views/AddDownloadDialog.xaml.cs` | تأكيد ContentDialog، وانتظار إغلاق الحوارات الأخرى؛ إنهاء صفحة الاكتشاف قبل إضافة التنزيل المباشر |
| `GNOME/Views/AddDownloadDialog.cs` | تأكيد Adw.AlertDialog على النافذة الأصلية، وانتظار استجابة المستخدم |
| واجهتا الإعدادات وملفاتهما الخلفية | قراءة الخيار وحفظه وعرض عنوانه ووصفه |
| `resources/po/ar.po` | ترجمة الإعداد ورسائل الأسباب والتأكيد |

المسارات المختصرة `Shared` و`WinUI` و`GNOME` تعني المشاريع `Nickvision.Parabolic.Shared` و`Nickvision.Parabolic.WinUI` و`Nickvision.Parabolic.GNOME`.

- يشمل الفحص تكرار URI داخل الطلب نفسه؛ يجمع التحذيرات ويعرض تفاصيل ثمانية روابط كحد أقصى، مع بيان وجود روابط أخرى.
- يأخذ الملفات من السجل بشرط وجودها، ومن التنزيلات المكتملة في الجلسة الحالية إذا كانت حالتها Success وملفها موجودًا. الملفات المحذوفة لا تمنع إعادة التنزيل.
- `_additionLock` يسلسل فحص الإضافات الجديدة وتأكيدها وإدخالها، حتى لا يتجاوز اكتشافان متزامنان الفحص. مسارا إعادة المحاولة والاستعادة السابقان يبقيان دون هذا التأكيد؛ ليس هذا منعًا مطلقًا لكل إضافة داخلية.
- الروابط والمسارات معزولة بـLRI/PDI في رسالة التأكيد، دون تغيير URI المخزّن.
- يعتمد الكشف على URI الذي يعيده اكتشاف الوسائط، ولا يمسح جميع ملفات الجهاز أو يقارن محتواها. إذا حُذف السجل أو انتهت مدته ولم يعد التنزيل في الجلسة، فلا يمكن اكتشاف ملف سابق منه. لا توجد قاعدة جديدة تحفظ سجلًا أبديًا.

### الاختبارات وحدود التحقق

أُضيف مشروع مستقل جديد `tests/duplicates/DuplicateDownloadChecks.csproj` و`Program.cs`، يضم ملف الكاشف مباشرة دون بناء التطبيق. يغطي تسع حالات: رابط جديد؛ رابط نشط؛ أسبقية النشط على الملف القديم؛ ملف موجود؛ ملف محذوف؛ تكرار داخل الطلب؛ رابطان مختلفان من الموقع نفسه؛ تجميع التحذيرات؛ اختيار فارغ.

أُضيفت وظيفة `duplicate-downloads` إلى `.github/workflows/plugins.yml`، وأصبح اسم المسار `Download and subtitle checks`. يرتبط هذا المسار أصلًا ببناء الإصدار، لذلك تعمل الاختبارات قبل توزيع إصدار عند الإذن بتسليم الكود. أمر الفحص على المشغّل:

```sh
dotnet run --project tests/duplicates/DuplicateDownloadChecks.csproj --configuration Release
```

نجحت الحالات التسع للكاشف وفحص الموارد محليًا عبر PowerShell وعلى Actions في البناء المصحح. لم يُبنَ التطبيق محليًا ولم تُختبر واجهاته أثناء التشغيل. اختبارات الكاشف لا تغطي وحدها التكامل مع الإعدادات وحوارات UI؛ يلزم تجربة الحالات التالية بعد بناء ناجح:

1. إعداد مفعّل افتراضيًا، وتعطيله ثم إعادة تشغيل التطبيق للتحقق من الحفظ.
2. رابط جارٍ، ورابط منتظر أو موقوف مؤقتًا، ثم تأكيد المتابعة والإلغاء لكل منها.
3. ملف سابق موجود، ثم حذف الملف وإضافة الرابط مجددًا، مع تغيير الوجهة أو نوع الملف.
4. قائمة وملف روابط فيهما عناصر جديدة ومكررة؛ التنبيه واحد، ورفضه لا يضيف جزءًا من الطلب.
5. اكتشافان سريعان متزامنان للرابط نفسه؛ لا يفوت الثاني التنبيه.
6. التنزيل المباشر أثناء صفحة الاكتشاف، وانتهاء اكتشاف سريع أثناء حوار آخر، وإغلاق النافذة أثناء انتظار التأكيد. تحقق من سياق UI وإغلاق Popup قبل عرض ContentDialog ومن عدم بقاء القفل منتظرًا.
7. السجل المعطّل أو المنتهي، والملفات الناقصة، وتغيّر حالة تنزيل أثناء قراءة السجل. راجع الوصول إلى مجموعات الخدمة إذا كشف اختبار التشغيل تزامنًا غير آمن.

## انهيار الماك: ما ثبت وما بقي مجهولًا

وصل تقرير انهيار من المستخدم في 2026-10-14 بعد أن أبلغ عن الانهيار عند التشغيل مباشرة. المسار `/Applications/BearDL.app/Contents/MacOS/Nickvision.Parabolic.GNOME.bin`، والجهاز `MacBookAir10,1`، والمعمارية ARM64، وmacOS `27.0.1 (26A434)` وفق التقرير.

- بدأ التطبيق عند `03:03:15` وانتهى عند `03:03:21` بتوقيت الرياض.
- `EXC_CRASH (SIGABRT)`، و`Termination Reason: SIGNAL, Code 6`، و`abort() called`، على الخيط الرئيسي.
- UUID الملف التنفيذي: `7cc2e3c7-b346-3a2b-98be-5784db3cf158`. يلزم مطابقة هذا UUID قبل محاولة فك رموز التقرير؛ لا تفترض أن نسخة المستخدم هي 2026.5.116 لأن التقرير لا يعرض الإصدار.
- إطارات التطبيق غير مفكوكة الرموز. إزاحاتها عن بداية الصورة `0x10457c000` هي: `111676`، `3888468`، `3887636`، `4619384`، `4621344`، `4620476`، `521772`، `4632292`، `4642916`، `4642648`، `2264524`، `8332708`.
- ظهرت GLib وGObject وGIO وPango وCairo وGraphene ومكتبات أخرى من مجلد Frameworks. لم تظهر GTK أو libadwaita في قائمة المكتبات المحمّلة. هذه قرينة على عطل أثناء التهيئة، وليست دليلًا على مكتبة مفقودة بعينها أو على سبب الانهيار.
- راجعنا سجل وظيفة ARM64 في البناء السابق؛ يؤكد تضمين `libgtk-4.1.dylib` و`libadwaita-1.0.dylib`. نجاح التغليف والتوقيع لا يثبت نجاح التحميل على جهاز المستخدم.
- لم نغيّر كود تشغيل الماك لمحاولة إصلاح غير مثبتة، ولم يصل خرج Terminal المطلوب حتى الآن. لا تدّعِ أن DMG أو أي من التعديلات المحلية حلّ الانهيار.

أرسل المستخدم التقرير الأصلي في مرفق نصي. نُقلت الأدلة التشخيصية الأساسية أعلاه لتظل متاحة دون الاعتماد على بقاء مرفقات المحادثة.

الخطوة المطلوبة على جهاز الماك: افتح Terminal ونفّذ من أي مجلد:

```sh
"/Applications/BearDL.app/Contents/MacOS/Nickvision.Parabolic.GNOME"
```

لمعرفة الإصدار دون تشغيل التطبيق:

```sh
/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' /Applications/BearDL.app/Contents/Info.plist
```

حلل الرسالة الأصلية وسلسلة الاستثناءات أو سبب رفض المكتبة قبل تعديل مسار التشغيل. الملفات المعنية بالتغليف: `resources/macos/publish-and-package.sh` و`Info.plist` و`.github/workflows/macos.yml`؛ البرنامج يبدأ من `Nickvision.Parabolic.GNOME/Program.cs` ويُنشَر Native AOT.


## التحقق المحلي وحدوده

نجح `git diff --check` وتحليل XAML كـXML وفحص أسماء عناصره، وفحص مفاتيح gettext مع السياق؛ 372 مدخلًا نشطًا دون مفاتيح مكررة. مفتاح `System` مع سياق Theme وبلا سياق ليس تكرارًا خاطئًا. نتائج الكاشف والموارد مذكورة في تقريرها؛ لا تعوّض عن تجميع التطبيق واختبار واجهاته.

بعض إصلاحات RTL السابقة خاصة بـWinUI. المرجع التاريخي ومواضعها في دليل المشروع والكوميتات. لا تنقلها إلى GTK بالتخمين أو تفترض أنها تُعمم تلقائيًا.

## ما لم يطلب المستخدم تنفيذه

اقتراحات ترتيب التنزيلات المنتظرة، والبحث في إعدادات Windows، والإعدادات المحفوظة، وتحسين السجل ورسائل الأخطاء لم تُختر للتنفيذ. اختار المستخدم فحص التكرار فقط. افحص الكود والواجهات قبل اقتراح ميزة باعتبارها غير موجودة.

## سجل بناء سابق للاحتفاظ بالأدلة

الأرقام التالية لقطة تاريخية، وليست إذنًا حاليًا أو وصفًا للنسخة الأخيرة. شرح الكود المقابل لها موجود في دليل الكوميتات.

## حالة التحقق في 2026-10-05 — سجل سابق

- آخر كوميت منشور: `d7ba4bd029c35f5b4110433951620175a235c025` على `main`؛ يتضمن إصلاح إعداد بناء Windows بعد كوميت المفاتيح `0b329e31`.
- [بناء 2026.5.112](https://github.com/2-sa/BearDL/actions/runs/37235951850) نجح على المنصات كلها.
- [الإصدار 2026.5.112](https://github.com/2-sa/BearDL/releases/tag/2026.5.112) منشور، ويشير إلى `824d3d4cd67a9df79850b6b28c9b0c8352970a1d`، قبل إصلاح اتجاه المفاتيح.
- [بناء 2026.5.113](https://github.com/2-sa/BearDL/actions/runs/37315983930): نجح تجميع Windows ARM64 في المحاولة الأولى، ثم فشل تنزيل FFmpeg برسالة `404 Not Found` في خطوة Installer؛ أُلغي x64 وتُجاوز النشر. تحققنا من وجود الملف نفسه وأعدنا الوظائف الفاشلة والملغاة بنفس الكوميت. فشلت المحاولة الثانية بعد نجاح تثبيت حزم MSYS2 على ARM64، أثناء حفظ الكاش (`paccache`، رمز الخروج 127)؛ أُلغي x64 أثناء توليد الكود الأصلي بسبب fail-fast. لم يُنشر الإصدار.
- إصلاح `.github/workflows/windows.yml` مأذون بعمل كوميت ورفعه: تعطيل كاش MSYS2 على ARM64، وضبط `fail-fast: false` لمنع إلغاء المنصة الأخرى، وإعادة محاولة تنزيل FFmpeg 5 مرات بفاصل 30 ثانية. اجتاز `git diff --check`؛ لم يُختبر هذا التعديل على Actions بعد. الرفع يبدأ بناء جديدًا برقم إصدار جديد؛ افحص `git log -1` وGitHub لمعرفة كوميت التسليم ونتيجة البناء.
- [بناء 2026.5.114](https://github.com/2-sa/BearDL/actions/runs/37321785437) نجح، ونُشر الإصدار من `d7ba4bd0`. تحققنا أن ملف البرنامج المثبت يحمل 2026.5.114 والكوميت نفسه؛ أكد المستخدم بالصور أن إصلاح المفاتيح السابق لم ينجح. لم تظهر عملية البرنامج في فحص العمليات الحالي، فلم نقرأ خصائص XAML الحية.


## خطوات الاستمرار

1. اقرأ `AGENTS.md` وهذه الوثيقة، ثم افحص Git؛ لا تُسقط التوثيق المحلي.
2. اتبع الطلب الجديد المحدد. فشل Windows وانهيار الماك قضيتان مختلفتان: الأول خطأ تجميع معلوم، والثاني يحتاج خرج Terminal لتحديد السبب.
3. عند الإذن بإصلاح وتسليم الكود، تحقق من نجاح اختبارات الكاشف وتجميع Windows والماك ولينكس. لا تعِد تشغيل إصدار قديم لتوزيع كود مختلف بالرقم نفسه.
4. اختبر الإشعارات والمنتقي وفحص التكرار، وسحب شريط العنوان وأزراره وتغيير حجم النافذة وDPI والعربية والإنجليزية. فحص `tests/windows/Verify-DownloadDialogLayout.ps1` قراءة لحدود الواجهة، وليس بديلًا عن الصور وتجربة النسخة المشغلة.
5. حدّث حالة هذه الوثيقة دون نسخها إلى وثيقة تسليم جديدة متداخلة. اجعل دليل المشروع للتاريخ والبنية، وتقرير الموارد للقياسات، ودليل الإصدار للتوزيع.

## CI storage policy — 2026-10-19

The user authorized applying the artifact storage policy to all owned repositories. Release packaging is now manual; focused checks stay automatic. Artifacts retain files for one day, with scheduled completed-run cleanup and preview mode. See PROJECT_MAP.md and the Artifact storage policy Actions run for source-specific verification; no new application release was published by this maintenance change.

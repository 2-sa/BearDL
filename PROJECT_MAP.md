# خريطة مسار الاسم والترجمة

هذه خريطة محدودة للمسار الذي روجع، وليست بديلًا عن دليل المشروع.
الحالة والإذن الساري في [docs/CURRENT_TASK.md](docs/CURRENT_TASK.md).

## [TECH_STACK]

- .NET 10، وNickvision.Desktop 2026.5.3، وgettext؛ واجهتا WinUI وGTK.
- `Nickvision.Common.targets` يجمع ملفات اللغات باسم `parabolic.mo`.

## [SYSTEM_FLOW]

- `UI-05`: فتح الإعدادات والصفحات العربية → استخراج نصوص مساعد التصميم عند بناء الترجمة،
  وعرض أسماء اللغات بحجم متناسق مع بقاء رموزها المخزنة، وتوحيد عرض القوائم وحقول الإدخال (`MinWidth="180"`)، ووضع سهم التوسيع في `SettingsExpander` على يمين مفتاح التبديل في RTL (`ToggleSwitchHelper.ApplyExpanders`)، وتطابق حواف الأشرطة والقوائم في RTL وLTR عبر `ScrollViewportHelper.ReserveScrollbarRight="20"` مع وراثة اتجاه `FlowDirection` الطبيعي دون كسر RTL، وتمرير `PanelUserInterface` صراحةً إلى `GlassThemeHelper.AttachSettingsCard` لضمان ظهور بطاقتي الشفافية وتصميم الواجهة دائمًا، وإخفاء شارة الاختصارات العائمة (`KeyboardAcceleratorPlacementMode.Hidden`) ومحاذاة قائمة المساعدة المنبثقة (`FlyoutHelp`) حسب اتجاه الواجهة.
- `TIKTOK-01`: اكتشاف وتنزيل مقاطع TikTok وأغلب المواقع المعروفة → ترقية `yt-dlp` المدمج إلى `2026.10.21` وDeno إلى `2.9.8`، مع إضافة `tiktok_fix` في `resources/yt-dlp-plugins/tiktok_fix` لتوليد ترويسات تجاوز `HTTP Error 403` ودعم روابط المشاركة مع `impersonate=True`، وتحديث `Format.cs` و`Media.cs` و`DiscoveryResult.cs` و`YtdlpExecutableService.cs` و`StringExtensions.TryExtractHttpUri` لدعم الصيغ المدمجة (Video+Audio)، والمنشورات متعددة الوسائط، واستخلاص الروابط من نصوص المشاركة، وحماية تحميل الصور المصغرة.
- `COPY-01`: نسخ ملف مكتمل في Windows → `FileClipboard.CopyFileAsync` يكتب قائمة ملفات Unicode
  بصيغة Shell مع تأثير COPY → إمكان لصق الملف نفسه واستمرار بياناته بعد إغلاق المصدر.
- `UI-04`: الضغط على «تنزيل» في اقتراح الحافظة على Windows → `ClipboardUse_Click` يلتقط
  الرابط ويخفي الاقتراح → `AddDownloadDialog.StartDownloadAsync` يقرأ Fast Download:
  عند تفعيله يبدأ التجهيز الخلفي والتنزيل بمساره الحالي؛ وعند تعطيله يبدأ الاكتشاف وخيارات التنزيل.
  القبول: ضغطة واحدة مع تفعيل الإعداد، دون ضغط زر الشريط العلوي أو تغيير نصه، مع بقاء فحص التكرار.
- `L10N-01`: بدء التطبيق → تهيئة AppInfo → TranslationService يشتق المجال
  من EnglishShortName → اكتشاف مجلدات اللغات → تحميل العربية من `ar/parabolic.mo`.
- النتيجة المطلوبة: ظهور العربية في قائمة اللغات وقراءة ترجماتها، مع بقاء
  اسم العرض BearDL. اختلاف مجال التهيئة عن اسم ملف MO يجعل اللغات غير مكتشفة.
- `THEME-01`: بدء التطبيق أو تغيير السمة في الإعدادات → `MainWindow.ApplyTheme()`
  يضبط `PreferredTheme` لشريط العنوان و`RequestedTheme` على `WindowRoot` (`Window.Content`)
  و`MainGrid` معًا → تحدّث `MicaBackdrop` و`PopupRoot` خلفية النافذة والقوائم المنبثقة
  وفق السمة المختارة بدل البقاء على سمة نظام Windows.
- `THEME-02`: اختيار سمة `Glass` («زجاجي») في الإعدادات → استدعاء `GlassThemeHelper.Apply`
  في WinUI (`ClearGlassBackdrop` بشفافية DWM افتراضية `50%` مع شريط تحكم حي `AttachSettingsCard`
  في الإعدادات + حواف زجاجية عاكسة + بطاقات عائمة) وفي GTK (`glass-mode` CSS مع `glass-theme.css`).
- `UI-01`: فتح النافذة الرئيسية على Windows → يفتح افتراضيًا على التصميم القديم الكلاسيكي (`Classic`)،
  مع توفير خيار **«تصميم الواجهة» (`Interface Layout`)** في الإعدادات (`GlassThemeHelper.AttachSettingsCard`)
  للتبديل الفوري الحيّ بين **القديم (الكلاسيكي)** و**الجديد (الحديث)** (`MainWindow.ApplyLayoutMode`).
- `UI-02`: بطاقة تنزيل Windows في كلا التصميمين → أزرار أيقونات `32×32` حسب حالة التنزيل،
  مع تلميحات وأسماء وصول مترجمة، والسجل داخل قائمة ⋯. بعد النجاح تظهر أزرار التشغيل والنسخ وفتح المجلد؛
  يعرض النسخ نتيجته بأيقونة وتلميح مؤقت دون توسيع الزر. القبول: مساحة أوسع للعنوان والتقدم،
  وسلامة الأزرار وقائمة السجل ولوحة المفاتيح في العربية RTL والإنجليزية LTR.
- `UI-03`: الانتقال إلى الإعدادات أو فتح السجل والحسابات وإضافة التنزيل و«عن البرنامج» على Windows
  → عناوين وأسطح وحقول وقوائم متسقة مع التصميم الجديد. الإعدادات بتبويبات علوية، والسجل والحسابات
  ببحث وأوامر منفصلة عن بطاقات النتائج، وحوار التنزيل بأقسام وتبويبات ضمن قيود عرضه الحالية.
  القبول: تشغيل الوظائف الحالية دون قص أو تمدد غير مقصود في RTL وLTR والثيمات الثلاثة وأحجام النوافذ المختلفة.

## [ARCHITECTURE]

- `Nickvision.Parabolic.WinUI/Helpers/FileClipboard.cs`: نقل الملف إلى حافظة Windows الأصلية
  مع ملكية الذاكرة والنافذة ومحاولات فتح محدودة؛ يستدعيه `DownloadRow` مع تسجيل أسباب الفشل.
  `tests/windows/ClipboardChecks.csproj` يضم المساعد الفعلي لفحص Shell على Actions قبل تغليف Windows.
- `resources/po/POTFILES` يشمل مساعد إعدادات التصميم؛ يفحص
  `tests/windows/Verify-TranslationCatalogs.ps1` وجود ترجماته العربية في ناتج MO المنشور.
- `HostApplicationBuilderExtensions.cs` في Shared يهيئ الهوية الداخلية الثابتة.
- `MainWindowController.cs` يضبط ShortName الظاهر إلى BearDL؛ لا يغيّر EnglishShortName.
- `Nickvision.Common.targets` يولد الكتالوجات للواجهتين.
- `tests/windows/Verify-TranslationCatalogs.ps1` يطابق المجال مع كل ملفات اللغات
  المنشورة ويقرأ وجود ترجمة عربية؛ يعمل بعد Publish وقبل إنشاء المثبّت على Actions.
- `Nickvision.Parabolic.WinUI/Views/MainWindow.xaml` و`MainWindow.xaml.cs`: الجذر الخارجي
  `WindowRoot` يحتفظ باتجاه LTR لمناطق النقر في شريط العنوان ويتلقى `RequestedTheme`
  مباشرةً لأن `MicaBackdrop` و`PopupRoot` يراقبان جذر `Window.Content` لا الشبكة الداخلية `MainGrid`.
- عزل الوضع الزجاجي (`Theme.Glass`):
  - `Nickvision.Parabolic.WinUI/Helpers/GlassThemeHelper.cs`: يحوي كامل منطق وألوان وطبقات الوضع الزجاجي لـWinUI 3 وشريط التحكم بنسبة الشفافية وخيار التبديل بين التصميم القديم والجديد.
  - `Nickvision.Parabolic.GNOME/Helpers/GlassThemeHelper.cs` و`Resources/glass-theme.css`: يحويان منطق وتنسيقات الوضع الزجاجي لـGTK4/libadwaita.
- مجلدات البيانات ومعرفات التطبيق والبروتوكول منفصلة عن نصوص العرض.
- `Nickvision.Parabolic.WinUI/Controls/DownloadRow.xaml` و`DownloadRow.xaml.cs`: تنسيق أزرار
  `UI-02` وتلميحاتها وأسماء الوصول وتبديل حالات الإيقاف المؤقت ونتيجة النسخ؛ أصبح النسخ يستخدم
  `FileClipboard` وصيغة Shell الأصلية، وتبقى أحداث التنزيل الحالية دون تغيير.
- `Nickvision.Parabolic.WinUI/Styles/InterfaceStyles.xaml`: موارد `UI-02` و`UI-03` المشتركة؛
  يُدمج في `App.xaml` ويستخدم موارد السمة الدلالية. تطبّقه صفحات Windows وحوارات الحسابات
  والمعالجة الفرعية، وبطاقتا إعداد الشفافية وتصميم الواجهة المنشأتان في `GlassThemeHelper`.

## [ORPHANS & PENDING]

- `UI-02` إلى `UI-05` و`COPY-01` (2026-10-23): نُفّذت ورُفعت في `8ccbf579` ونُشرت في
  [2026.5.136](https://github.com/2-sa/BearDL/releases/tag/2026.5.136). نجحت فحوص الحافظة الفعلية
  على Windows x64 وARM64 في [40004052925](https://github.com/2-sa/BearDL/actions/runs/40004052925)،
  ثم نجح [40004247391](https://github.com/2-sa/BearDL/actions/runs/40004247391) بفحوصه وبناء المنصات الست،
  ومنه تجميع Windows وفحص ترجمات التصميم في الكتالوج المنشور. تحققت أصول الإصدار الأحد عشر والوسم.
  بقي القبول البصري للصفحات المصححة في RTL/LTR والثيمات المختلفة وأحجام النافذة وDPI، وتجربة
  زر الحافظة بإعداد Fast Download المفعّل والمعطّل والقوائم والتكرار، والنسخ واللصق في تطبيق المستخدم،
  والتحديث من نسخة سابقة. صور «خطأ» القديمة لا تحدد الاستثناء الأصلي؛ المسار الجديد يسجل سببه.
  المعاينة المولدة ليست إثباتًا بصريًا للتنفيذ. لا يُبنى أو يُنشر إصدار آخر لمجرد تسجيل هذه النتائج.
- `L10N-01`: نُفّذ فحص الحزمة `Verify-TranslationCatalogs.ps1` ونجح على Actions في البناء
  `38990949083` (`2026.5.124` من الكوميت `c17c224d`). أكدت لقطة المستخدم في 2026-10-18
  عودة الواجهة العربية واتجاه RTL وظهور الاسم `BearDL`.
- `UI-01`: أُعيد التصميم القديم كوضع افتراضي وأُضيف خيار «تصميم الواجهة» في الإعدادات، وحُل انهيار التبديل بتأجيل تغيير الصفحات عبر DispatcherQueue، مع إعادة تصميم الواجهة الحديثة بشكل موحد وإضافة زر نسخ الملف المكتمل للحافظة محليًا؛ جاهز للرفع.

## Artifact storage policy — 2026-10-19

- `ARTIFACT-01` is implemented in `.github/workflows/artifact-maintenance.yml` and `.github/scripts/artifact-policy.cjs`; behavioral checks are in `.github/scripts/artifact-policy.test.cjs`.
- Direct artifact uploads retain files for one day. Policy checks also reject future upload steps that omit this limit. Where present, Flatpak's implicit upload is replaced by an explicit one-day upload.
- Every six hours, maintenance deletes expired (>24h) or superseded artifacts from completed runs only. Commit-suffixed names are grouped by platform; current runs are protected and checked again immediately before deletion. Manual dispatch defaults to preview-only. Maintenance never modifies release assets or caches.
- Distribution builds run on explicit manual/release triggers where separated from verification. Existing tests remain automatic; site/container deployment and platform-managed delivery retain their existing purpose. Publishing requires its existing release trigger or an explicit manual publishing choice.
- Verification: policy behavior and workflow checks run on GitHub-hosted Ubuntu for this source commit; see the **Artifact storage policy** Actions run attached to the commit for its actual result. No application tests, builds, downloads, or installations were run on the user's device. Application packaging/updater behavior was not exercised by a policy-only check.
- Pending: GitHub may take 6–12 hours to recalculate storage after deletions. Existing artifacts do not receive shorter expiry retroactively; scheduled maintenance applies the same policy to them. Quota recovery and a future authorized release/upload must be observed separately. Scheduled workflows can be delayed or disabled by GitHub after inactivity; one-day upload retention remains the primary limit.

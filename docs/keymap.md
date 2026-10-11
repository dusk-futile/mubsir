# Keys and voice commands / المفاتيح والأوامر الصوتية

Basira keeps NVDA's habits: **a single Ctrl press only stops speech**, and Basira never uses NVDA's Insert or Caps Lock keys. Keys are bound by their physical position, so they work the same with the Arabic, English or French keyboard layout.

يحافظ بصيرة على عادات NVDA: **ضغطة Ctrl واحدة توقف الكلام فقط**، ولا يستخدم بصيرة مفتاحي Insert وCaps Lock الخاصين بـ NVDA. المفاتيح مربوطة بمكانها على اللوحة، فتعمل بالطريقة نفسها مع اللغة العربية أو الإنجليزية.

## Basira keys / مفاتيح بصيرة

| Keys | What it does | المفاتيح | ماذا يفعل |
|---|---|---|---|
| **Hold the talk key** (default: Right Ctrl; chosen at setup) | Talk in any dialect or English; release to send. If another key is pressed during the hold, the recording is dropped and the key works as usual (a slow Ctrl+C still copies). | **اضغط مع الاستمرار على مفتاح التحدث** (Ctrl الأيمن، ويمكن اختيار غيره) | تكلّم بلهجتك أو بالإنجليزية، ثم اترك المفتاح ليُرسَل الطلب. |
| **Right Ctrl twice quickly** (within 400 ms) | Command layer: a tone, then one of the keys below within 3 s. Any other key leaves the layer and works as usual. | **Ctrl الأيمن مرتين بسرعة** | طبقة الأوامر: نغمة، ثم أحد المفاتيح التالية خلال ثلاث ثوانٍ. |
| layer **Space** | Read from here (the whole document or window). | الطبقة ثم **المسافة** | اقرأ من هنا حتى النهاية. |
| layer **Left arrow / Right arrow** | Previous / next sentence (rewind / skip). | الطبقة ثم **السهم الأيسر / الأيمن** | الجملة السابقة / التالية. |
| layer **Up arrow / Down arrow** | Previous / next paragraph. | الطبقة ثم **السهم الأعلى / الأسفل** | الفقرة السابقة / التالية. |
| layer **. (period)** | Repeat the last sentence; press twice for the whole last answer. | الطبقة ثم **النقطة** | كرّر آخر جملة؛ ومرتين: الإجابة الأخيرة كاملة. |
| layer **Enter** | Type a request instead of speaking. | الطبقة ثم **Enter** | اكتب طلبك بدل أن تتكلم. |
| layer **Esc** | Stop Basira's current task. | الطبقة ثم **Esc** | أوقف المهمة الحالية. |
| layer **F1** | Help, and the key explorer (press any key to hear what it does). | الطبقة ثم **F1** | المساعدة، ومستكشف المفاتيح. |
| **During a task:** Right Ctrl once, or Esc | Stop the task at once. | **أثناء المهمة:** Ctrl الأيمن مرة واحدة، أو Esc | أوقف المهمة فورًا. |
| **During a task:** any other key | Pause ("paused: Enter to continue, Escape to cancel"). | **أثناء المهمة:** أي مفتاح آخر | إيقاف مؤقت: Enter للمتابعة، وEsc للإلغاء. |
| **Confirmation prompt:** Enter / Esc | Yes / no. Saying "نعم" or "yes" also works; 30 s of silence cancels; "." repeats the question. | **عند سؤال التأكيد:** Enter / Esc | نعم / لا. ويمكن قول «نعم»، والسكوت ثلاثين ثانية يلغي. |
| **Always-on stop key** | Works even if the keyboard hook stops; chosen in the M0 spike (see spikes.md). | **مفتاح الإيقاف الدائم** | يعمل دائمًا؛ يُحدَّد بعد التجارب الأولى. |

While Basira is reading, the command layer stays open for the arrow keys, so he can keep pressing Left arrow to go back sentence by sentence.

## Voice commands / أوامر صوتية

These are examples, not a fixed list: Basira understands free phrasing. How well each dialect is understood is measured on his own voice in M1 (Libyan has not been tested by anyone yet).

هذه أمثلة وليست قائمة ثابتة؛ يفهم بصيرة الكلام الحر بلهجات مختلفة.

| Request | Egyptian | Levantine | Gulf | Libyan | Moroccan | MSA / English |
|---|---|---|---|---|---|---|
| Read the screen | اقرالي اللي على الشاشة | اقرالي شو في عالشاشة | اقرا لي وش في الشاشة | اقرالي شن فيه في الشاشة | قرا ليا شنو كاين فالشاشة | اقرأ لي الشاشة / Read the screen |
| Scroll down | انزل تحت | انزل لتحت | نزّل تحت | انزل لوطا | هبط لتحت | مرّر إلى الأسفل / Scroll down |
| Go back a sentence | رجّع الجزء اللي فات | رجّع الجملة اللي قبل | رجّع الجملة اللي قبل | رجّع الجملة اللي قبل | رجع الجملة اللي قبل | أعد الجملة السابقة / Go back |
| Search the web | افتح جوجل وابحث عن مواعيد القطر | افتح غوغل ودوّر على أخبار الطقس | افتح قوقل ودوّر على مواقيت الصلاة | حل قوقل وفتش على أسعار الذهب | حل ليا جوجل وقلّب على وصفة الكسكس | افتح جوجل وابحث عن… / Search Google for… |
| Who messaged me? | مين بعتلي على واتساب؟ | مين بعتلي عالواتساب؟ | منو أرسل لي على الواتساب؟ | منو بعثلي على الواتساب؟ | شكون صيفط ليا فالواتساب؟ | من أرسل لي على واتساب؟ / Who messaged me? |
| What is this book about? | الكتاب ده بيتكلم عن إيه؟ | هالكتاب عن شو بيحكي؟ | هالكتاب وش يتكلم عنه؟ | الكتاب هذا يتكلم على شن؟ | هاد الكتاب على شنو كيهضر؟ | عمّ يتحدث هذا الكتاب؟ / What is this book about? |
| Slower, please | اتكلم أبطأ شوية | احكي أبطأ شوي | تكلم على مهلك | اتكلم بالشوية | هضر بشوية | تحدّث ببطء / Speak slower |

More requests: «افتح نافذة تانية بنفس الصفحة» (a new window with the same page), «حدّث الجهاز» (update the PC), «لخّص الصفحة» (summarize the page), «اقرأها كرياضيات» (read it as math), «تهجّاها» (spell it), «شغّل الوضع الخاص» (private mode: nothing leaves the PC), «الإعدادات» (spoken settings menu: speed, verbosity, talk key, budget, pause), «ماذا تفعل؟» (what are you doing?), «احفظ تقريرًا لعائلتي» (save a report for my family).

## NVDA basics / أساسيات NVDA

NVDA is the screen reader under Basira. He should practise these first, in week 1. "NVDA" below means the NVDA key: **Insert** (also Caps Lock if it is turned on in NVDA's keyboard settings).

| Keys | What it does | ماذا يفعل |
|---|---|---|
| Ctrl | Stop speech | يوقف الكلام |
| Shift | Pause / resume speech | يوقف الكلام مؤقتًا أو يستأنفه |
| Tab / Shift+Tab | Next / previous control | العنصر التالي / السابق |
| Alt+Tab | Switch window | التنقل بين النوافذ |
| Alt+F4 | Close the window | إغلاق النافذة |
| Windows key | Start menu: type an app name, then Enter | قائمة ابدأ: اكتب اسم البرنامج ثم Enter |
| Alt+Shift | Switch the keyboard language (Arabic / English) | تبديل لغة لوحة المفاتيح |
| NVDA+Space | Browse mode / focus mode in web pages | التبديل بين وضع التصفح ووضع التركيز |
| H, K, F, B (browse mode) | Next heading, link, form field, button | العنوان، الرابط، الحقل، الزر التالي |
| NVDA+F7 | List of links and headings | قائمة الروابط والعناوين |
| NVDA+T | Read the window title | قراءة عنوان النافذة |
| NVDA+Tab | Read the focused item again | قراءة العنصر الحالي |
| NVDA+Down arrow (desktop) / NVDA+A (laptop) | Read everything from here | قراءة كل شيء من هنا |
| NVDA+F12 | Time (twice: date) | الوقت (مرتين: التاريخ) |
| NVDA+Shift+B | Battery | البطارية |
| NVDA+1 | Input help: press keys to hear their names; NVDA+1 again to leave | مساعدة الإدخال: اضغط أي مفتاح لتسمع اسمه |
| NVDA+N | NVDA menu | قائمة NVDA |
| NVDA+Q | Quit NVDA (asks first) | إغلاق NVDA |
| Ctrl+Alt+N | Start NVDA (**verify that the installer created this desktop shortcut**) | تشغيل NVDA |

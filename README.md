# L2 Marker

Batch marker for NCFE Level 2 Certificate in Understanding Coding workbooks. Loads a folder of
student `.docx` submissions, marks each one against the official model answers for a unit using
the Claude API, and generates a filled-in "Assessor Feedback to Learner" marksheet next to each
student's file.

## Setup

1. Open `L2Marker.sln` in Visual Studio (or run `dotnet build` from this folder) and run the app.
2. **File → Settings...** and paste in your Claude API key. Use **Test** to confirm it works.
3. Check the **Reference folder** points at your `Workbooks - All Units` folder — the one
   containing `NCFE_L2_UnitN_ModelAnswers.docx` and `Unit N Assessor Feedback to Learner.docx`
   for each unit. It defaults to the folder already in use for this qualification.
4. Settings are saved to `settings.json` next to the executable. **This file holds your API key in
   plain text** — treat it like a password file (don't commit it to a public repo, don't share the
   build folder).

## Using it

1. Pick the **Unit** (1–5) from the dropdown.
2. **Add Files...** or drag-and-drop the student submissions (20–25 at a time is fine). The
   **Learner** column is editable — click into a row and fix the name if it was pulled wrong from
   the file; whatever's in that column when you start marking is what's used in the prompt, the
   filled-in marksheet, and the output filename.
3. **Start Marking**. Each submission is sent to Claude and marked against every criterion for
   that unit as ACHIEVED / NOT ACHIEVED, with a short assessor comment per criterion plus overall
   feedback and further actions, all written second-person ("you...") direct to the learner.
4. A completed marksheet (`<Learner Name> Unit N FB.docx`) is written next to each student's
   submission by default, or into one folder of your choice — set **Output folder** in Settings
   (leave it blank to go back to the default). Double-click a row (or **View Details**) to review
   the full breakdown on screen before you sign anything off.

Learner and assessor signatures/dates are left blank on purpose — this tool drafts the marking,
it doesn't sign it off. Review the generated marksheets before treating them as final.

## How marking works

For each unit, the app extracts the full text of the model answers document and the list of
criteria (id + question) straight out of the blank marksheet template — nothing is hard-coded, so
it re-derives both automatically the first time you use a unit.

For each student, it extracts the submission's text (paragraphs and tables, in reading order —
screenshots/images embedded in later units are not read) and sends it to Claude along with the
unit's rubric, asking it to return a strict achieved/not-achieved decision per criterion via a
forced tool call (so the response is always clean structured data, not free text to parse).
Feedback — the per-criterion comments, the overall summary, and further actions — is written
directly to the learner as "you", never by name or as "the learner"/"the student".

## Cost efficiency

- **Model**: defaults to Claude Haiku 4.5, which is inexpensive and more than accurate enough for
  comparing a short answer against a model-answer rubric. You can switch to Sonnet 5 (or any model
  id) in Settings if you want more nuanced judgement on borderline answers.
- **Prompt caching**: the unit's rubric (instructions + full model answers, identical for every
  student in a batch) is sent as a cached system block. Only the first student in a run pays full
  price for it — the rest re-use the cached version at a fraction of the input cost. Running a
  batch of 20–25 students against one unit is exactly the case this is designed for.
- **One call per student**: keeps failures isolated (one bad file doesn't affect the rest of the
  batch) and keeps each request small.

## Cost tracking

Anthropic's API has no "check my balance" endpoint, so spend is estimated locally: every response
reports exactly how many input/output/cache tokens it billed for, and Settings lets you enter the
$/million-token rates for your chosen model (a **Use standard pricing** button fills in the known
rates for Haiku 4.5 and Sonnet 5). From that:

- The main window's status bar shows a running lifetime total (persisted in `settings.json`) and,
  after each batch, roughly what that run cost.
- Each row in the grid, and the **View Details** dialog, show that student's own cost and raw
  token counts.
- An optional **Budget (USD)** in Settings warns you once as you pass 80% of it and once more if
  you go over — visibility, not a hard cap; runs are never blocked.
- **Reset Spend** in Settings zeroes the lifetime total (e.g. at the start of a new term).

Treat the figure as a guide, not the invoice — it's a local estimate, not a query against your
actual Anthropic billing.

## Known limitations

- Images/screenshots inside a submission (e.g. code screenshots in later units) are not read —
  only text and tables. If a unit relies heavily on screenshots, review those criteria manually.
- Name detection looks for a "Name" table cell first, falling back to the filename, and isn't
  always right. The Learner column is editable — check it after adding files and fix anything odd
  before marking (the grid locks once a run starts, so edit before clicking Start Marking).
- This is an assistive tool, not an auto-signing system — always spot-check the AI's marking,
  especially borderline achieved/not-achieved calls, before treating a unit as signed off.

## License

Released under [Creative Commons BY-NC-SA 4.0](LICENSE.md) — see **Help → About L2 Marker** in the
app, or [LICENSE.md](LICENSE.md), for the summary and link to the full text.

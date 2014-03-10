Write Debugger friendly Data Models

Fixing bugs in production stinks!  One of the many reasons is that it takes time away from feature development.  While fixing bugs I often can't help but make a calculation in my head:
<pre>
X time fixing this bug = - X time of feature development = Y lost valuable features
</pre> 
With this equation in mind I try to find ways to minimize time needed to fix bugs.  Of course I try to eliminate as many bugs as possible in my code.  But for those that do seep out, I try to help myself by creating data models that make debugging easier. 

I generally use relational databases for persisting data.  So I'll explain my implementations in SQL, but these concepts can be applied to many technologies.  I find I create data models that make debugging easier when I try accomplish two goals by following three guidelines.

<strong>Goals:</strong>
<ul>
	<li>Allow developers to be able to difinitively understand how, by whom, and when a record was created.</li>
	<li>Allow developers to be able to quickly understand how, by whom, and when a record was created.</li>
</ul>

<strong>Guidelines</strong>
<ul>
	<li><strong>Make your datamodels deterministic</strong></li>
	<li><strong>Place meta information in strategically placed meta columns<strong></li>
	<li><strong>Place meta information that doens't naturally fit into meta columns in logging tables</strong></li>
</ul>

<strong>Make your data models deterministic</strong>
The only way to allow developers to difinitively be able to understand how records are created is to make your data models deterministic.  Developers should be able to look at the data model in any state and be able to difinitively deduce how each record was created...well developers should be able to difinitively determine how each interesting record was created.  Certainly, there will be less sensitive parts of your data model which don't need to uphold this property.  Generally, immutabliity translates to determinism.  So make sensitive parts of your data model deterministic by leveraging immutibility.

I'll explain three design patters that work together to guarentee determinism by leveraging immutibility.  These design patterns came from years of iteration and insight from many talented engineers at Applied Predictive Technologies.  So please don't give me too much credit.  I'm simply sharing.

In our product we make a lot of mathematical calculations.  Generally, these calculations execute interesting algorithms that can run from 30 seconds to 5 hours.  The general structure of these calculations is that we pass in a set of settings to a calculation process which spits out a set of results.  I'll use a simple example to explain how we use the first of three design patterns to capture the calculation information.

<strong>
Example: Calculate likelyhood that each swimmer will win a race.  We use information about a swimmer's expected race time, race time variance, and boost from crowd size (we assume that certain swimmers swim better/worse with larger crowds because of nerves and adreneline) to calculate his likelyhood of winning the race.
</strong>

<strong>Components</strong>
<ul>
	<li>Aggregate Settings</li>
	<li>Individual Result Settings</li>
	<li>Calculation Run</li>
	<li>Results</li>
</ul>

<strong>Aggregate Settings</strong>
We'll need to know the type of race (100 meter fly, 200 meeter IM, etc.) to get each swimmer's expected time and the location of the pool to get each swimmer's boost from home pool advantage.  We'll create a table with immutable records to store this information.

dbo.swimRaceSettings
swimRaceSettingId
poolId
swimRaceType

If you're paying careful attention, you'll notice that pools must be immutable for swim race settings to be immutable.  For this example, we'll assume that pools in our data model are immutable.  A bit later on I'll explain another nice pattern we use to keep potentially changing constructs, like pools, preserve immutable characteristics.

<strong>Individual Result Settings</strong>
We need to know the swimmers in the race to know who to make our calculations for and so we can compare the racers to calculate each racer's chance of beating all other racers.  We'll create a table, again with immutable records, to store this information.

dbo.swimRaceSwimmerSettings
swimRaceSwimmerSettingId
swimRaceCalculationId
swimmerId

If you're paying careful attention, you'll notice that I've associated an individual result with a "swim race calculation" (defined below) rather than a setting.  I associate individual results with the calculation rather than the aggregate setting because it gives us flexibility to use aggregate settings for different calculations.  For our example imagine we want to calculate two heats of the 100 meter fly.  We can reuse our aggegate settings for different individual swimmers.  Also, like pools, swimmers must be immutable.  Later I'll explain a nice way to make swimmers, who impove over time, preserve immutable properties.

<strong>Calculation Run</strong>
We want to store information about the actual calculation and the settings used for our calculation.  We'll create a table, again with immutable records, to store this information.

dbo.swimRaceCalculation
swimRaceCalculationId
swimRaceSettingId

<strong>Individual Results</strong>
Finally, we need a place to store the results.  We'll create a table, again with immutable records, to store the results.

dbo.swimRaceSwimmerResults
swimRaceSwimmerSettingId
swimRaceRunId
percentChanceOfWinning

Now we've linked the calculations for each swimmer to their respective swimmer settings and to the aggregate swim race settings through the calculation table.


Ok, so that's it.  Four simple immutable tables which hold information about settings, calculations, and results.  This pattern allows us to keep our data models deterministic.  It's easy for developers to determine what settings were used to calculate results.  And it's easy for developers to reproduce a calculation which should reproduce the same results.  These two abilities really help debugging because it makes identifying the problematic settings quickly.  And it makes investigataing the problematic settings easier.

So I mentioned earlier that pools and swimmers must be immutable to achieve the full power of this data model.  Unfortunately pools and swimmers are not immutable concepts.  Swimmers for example get stronger as the train and pools undergo construction to build bigger fan sections.  This leads us towards our second useful design pattern.

We'll express pools and swimmers in immutable terms by separating the entities into two parts.  In one table we'll store meta information about swimmers and pools.  This is information like names, date created, age, etc. that does not effect calculations.  In a second table we'll store snapshots of a swimmer or pool's attributes that effect calculations.  I like to think of these tables as definition tables because the define the characteristics of an entity at a point in time.

dbo.swimmer
swimmerId
swimmerDefinitionId
name
birthday

dbo.swimmerDefinition
swimmerDefinitionId
responseToHomeCrowdFactor

dbo.swimmerExpectedRaceTimes
swimmerDefinitionId
swimRaceType
expectedTime
variance

dbo.pool
poolId
poolDefinitionId
complexName

dbo.poolDefinition
poolDefinitionId
crowdSize

Notice that swimmers point to their respective swimmer definitions and pools point to their respective pool definitions.  If we ever want to record a swimmer's improvement or pool construction we create a new swimmer definition or pool definition.  Then we point the swimmer to the new swimmer definition or pool to the new pool definition.  Furthermore, now that we have immutable constructs to express information necessary for swim race calculations, we'll use swimmer definitions and pool definitions rather than swimmers and pools.  You can see this change in the final data model layout at the end of this post. 

<strong>Place meta information in strategically placed meta columns<strong>
Up to this point we haven't provided any meta information about how long a calculation took, which user kicked off the calculation, or if a user deleted a record.  So let's add that information with one thought in mind.  We want to be able to quickly and definitively determine what and how a record was created.  And this leads us to the third data model.  We'll store meta information about similiar types in common tables.

We'll store common meta information about calculations in a base calculations table.

dbo.calculations
calculationId
calculationType
status
dateCreated
dateStarted
dateCompleted
duration
userId

dbo.swimRaceCalculation
calculationId
swimRaceSettingsId

We like to store when, by whom, and how entities were created.

dbo.entities
entityId
entityType
dateCreated
createdBy
dateDeleted
deletedBy
dateLastModified
modifiedBy

dbo.swimmer
entityId
swimmerDefinitionId
name
birthday

dbo.pool
entityId
poolDefinitionId
complexName

<strong>Place meta information that doens't naturally fit into meta columns in logging tables</strong>
Finally, there is usally information that we determine is not important enough to take up space in the main data model tables.  If this infomation provides useful debugging information it can be important to include this information in log tables.  Keep in mind that our goal is to help developers quickly and dinfinitively determine how a record in the database was created.  With our data model we don't log when swimmer or pool definitions have changed.  Perhaps we want to create a logging table which records when a swimmer or pool's definition was updated.  In other words, we may want to remember every time a swimmer got stronger or a pool underwent construction.

dbo.entityDefinitionChanges
entityId
oldDefinitionId
newDefinitionId
dateChanged
changedBy
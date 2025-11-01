<?php

namespace App\Http\Controllers;

use App\Tag;
use App\Http\Requests\Tags\CreateTagRequest;
use App\Http\Requests\Tags\UpdateTagRequest;
use Illuminate\Http\Request;

class TagsController extends Controller
{



    //---------------------------------------------View All tags
    /**
     * Display a listing of the resource.
     *
     * @return \Illuminate\Http\Response
     */
    public function index()
    {
        return view('tags.index')->with('tags', Tag::all());
    }






    //---------------------------------------------Create New Tag
    /**
     * Show the form for creating a new resource.
     *
     * @return \Illuminate\Http\Response
     */
    public function create()
    {
        return view('tags.create');
    }
    /**
     * Store a newly created resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @return \Illuminate\Http\Response
     */
    public function store(CreateTagRequest $request)
    {
        $this->validate($request, [
            'name' => 'required|unique:tags',
        ]);

        Tag::create([
            'name' => $request->name
        ]);

        session()->flash('success', 'Tag '.($request->name).' created succesfully');

        return redirect(route('tags.index'));
    }








    //---------------------------------------------View A Tag
    /**
     * Display the specified resource.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function show($id)
    {
        //
    }





    //---------------------------------------------Edit A Tag
    /**
     * Show the form for editing the specified resource.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function edit(Tag $Tag)
    {
        return view('tags.create')->with('tag', $Tag);
    }
    /**
     * Update the specified resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function update(UpdateTagRequest $request, Tag $Tag)
    {
        $Tag->update([
            'name' => $request->name,
        ]);

        session()->flash('success', 'Tag '.($request->name).' updated succesfully');

        return redirect(route('tags.index'));
    }





    //---------------------------------------------Delete A Tag
    /**
     * Remove the specified resource from storage.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function destroy(Tag $Tag)
    {
        $Tag->delete();
        session()->flash('success', 'Tag '.$Tag->name.' deleted successfully');
        return redirect(route('tags.index'));
    }
}

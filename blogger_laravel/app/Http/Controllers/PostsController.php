<?php

namespace App\Http\Controllers;

use App\Http\Requests\Posts\CreatePostRequest;
use App\Http\Requests\Posts\UpdatePostRequest;
use App\Post;
use App\Category;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Storage;


class PostsController extends Controller
{


    public function __construct(){
        $this->middleware('categorized')->only(['create', 'store', 'edit', 'update']);
    }


    //-------------------------------------------View All Posts
    /**
     * Display a listing of the resource.
     *
     * @return \Illuminate\Http\Response
     */
    public function index()
    {
        return view('posts.index')->with('posts', Post::all());
    }




    //--------------------------------------------Add New Post
    /**
     * Show the form for creating a new resource.
     *
     * @return \Illuminate\Http\Response
     */
    public function create()
    {
        return view('posts.create')->with('categories', Category::all());
    }
    /**
     * Store a newly created resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @return \Illuminate\Http\Response
     */
    public function store(CreatePostRequest $request)
    {
        $image = $request->image->store('posts'); //-------------Store Image, Public Config
        Post::create([
            'category_id' => $request->category_id,
            'title' => $request->title,
            'description' => $request->description,
            'content' => $request->content,
            'image' => $image,
            'submitted_at' => $request->submitted_at
        ]);

        session()->flash('success', 'post '.$request->title.' created successfully');

        return redirect(route('posts.index'));
    }





    //----------------------------------------------View A Post
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





    //-----------------------------------------------Edit A Post
    /**
     * Show the form for editing the specified resource.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function edit(Post $post)
    {
        return view('posts.create')->with('post', $post)->with('categories', Category::all());
    }
    /**
     * Update the specified resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function update(UpdatePostRequest $request, Post $post)
    {
        $data = $request->only(['category_id', 'title', 'description', 'content' , 'submitted_at']);
        if($request->hasFile('image')){
            $image = $request->image->store('posts');
            $post->deleteImage();
            $data['image'] = $image;
        }

        $post->update($data);
        session()->flash('success', 'post '.$request->title.' updated successfully');

        return redirect(route('posts.index'));
    }





    //-----------------------------------------------Trash And Delete A Post
    /**
     * Remove the specified resource from storage.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function destroy($id)
    {
        $post = Post::withTrashed()->where('id', $id)->firstOrFail();
        $trashed = $post->trashed();
        $route = ($trashed?'trashed-posts':'posts').'.index';
        $message = 'post '.$post->title.($trashed?' deleted':' trashed').' successfully';
        if($trashed){
            $post->deleteImage();
            $post->forceDelete();
        }
        else{
            $post->delete();
        }
        session()->flash('success', $message);
        return redirect(route($route));
    }






    //-------------------------------------------View All Trashed Posts
    /**
     * Display a listing of the resource.
     *
     * @return \Illuminate\Http\Response
     */
    public function trashed()
    {
        return view('posts.index')->with('posts', Post::onlyTrashed()->get())->with('trashed', 'yes');
    }




    //-------------------------------------------Restore A Trashed Post
    /**
     * Remove the specified resource from storage.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function restore($id)
    {
        $post = Post::withTrashed()->where('id', $id)->firstOrFail();
        $post->restore();

        session()->flash('success', 'post '.$post->title.' restored successfully');
        return redirect(route('posts.index'));
    }
}
